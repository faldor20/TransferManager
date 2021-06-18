module JobManager.Access

open JobManager.Main
open System
open System.Collections.Generic
open SharedFs.SharedTypes
open System.Linq
open RequestHandler
open LoggingFsharp

//======Basic overview of Jobdatabase access=====
//There will be two main loops managing the jobs. One loop gives new tokens the other runs new jobs.
//These jobs should really only be run occasionaly. The reasons they should be run are listed below.
//1. A new job being added should recursiveley run "get next token" untill it has got all tokens it needs and can be run or the next desired token is unavailable
//2. A job being removed should go through each token being put back in reverse order and attempt to give each to the most prioritized job that desires it. if it cannont be given it is returned to the tokenList
//those should be the only two times actions requiring iterating over large numbers of jobs and/or tokens needs to be taken



let private runJob jobDB jobsToRun =
    Lgdebug "Running jobs {@jobs}" jobsToRun
    jobsToRun
    |> List.iter (fun (x, i) ->
        jobDB.RunningJobs.Add(i)
        jobDB.RunJob x i |> Async.Start)

let removeWaitingJob jobDB (source: Source) sourceID (id: JobID) =
    match jobDB.JobOrder.Remove(sourceID) with
    | true -> ()
    | false -> Lgerror2 "Tried to remove job {@id} from source {@srcID} that should have been there but wasn't" id sourceID
    //this removes the job from the source list
    let i =
        source.Jobs.FindIndex(Predicate(fun x -> x.ID = id))
    match i with
    | (-1) -> Lgerror2 "Tried to remove job {@jobid} from source {@srcid} that should have been there but wasn't" id sourceID
    | a -> source.Jobs.RemoveAt a


///Trys to run the job. if it has all the required tokens it will run the job.
///This will remove the job from it's source, and remove the first instance of the source from the jobOrder.
///Then it will add the job to runningjobsList and run the job
///Can run a job out of order... but if only called when a job becomes available order should be preserved
let tryrunJob (jobDB: JobDataBase) id =

    Lgdebug "Trying to run job {@id}" id
    let job = JobList.getJob jobDB.JobList id
    if job.TakenTokens.Length > 1 then
        let sourceID = List.last job.TakenTokens
        let source = jobDB.Sources.[sourceID]
        if source.RequiredTokens = job.TakenTokens then
            match jobDB.JobOrder.Remove(sourceID) with
            | true -> ()
            | false ->
                Lgerror2
                    "Tried to remove job {@id} from source {@srcID} that should have been there but wasn't"
                    id
                    sourceID
            //this removes the job from the source list
            let i =
                source.Jobs.FindIndex(Predicate(fun x -> x.ID = id))
            match i with
            | (-1) ->
                Lgerror2
                    "Tried to remove job {@jobID} from source {@srcID} that should have been there but wasn't"
                    id
                    sourceID
            | a -> source.Jobs.RemoveAt a

            Lgdebug2 "Running Specifically job {@jobID} from source {@srcID} " id sourceID
            runJob jobDB [ (sourceID, id) ]



let tryRunJobs (jobDB: JobDataBase) =
    let jobsTorun =
        JobOrder.takeJobsReadyToRun jobDB.JobOrder jobDB.Sources
    runJob jobDB jobsTorun






let getUIData (jobDB) =


    let { JobOrder = jobOrder; JobList = jobList; Sources = sources; FinishedJobs = finishedJobs;
          RunningJobs = runningJobs; TransferDataList = transferDataList } =
        jobDB

    let orderdIDs =
        JobOrder.countUp jobOrder
        |> Seq.map (fun (id, index) ->
            { JobID = sources.[id].Jobs.[index].ID
              RequiredTokens = sources.[id].RequiredTokens.ToArray() })
    //this only works on jobs that have all the tokens they need.
    //this is becuase the SourceIDList can be usefull in filtering at the ui end
    //we can use taken tokens becuase running and finished jobs will have all their tokens,
    // it is easier than  getting the requiretokens from the source for each token
    let convertToOut (li: List<JobID>) =
        li.Select(fun x ->
            { JobID = x
              RequiredTokens = jobList.[x].TakenTokens.ToArray() })
    //TODO: t
    let jobIDs =
        (convertToOut finishedJobs).Concat(convertToOut runningJobs).Concat(orderdIDs).Reverse()

    (jobIDs.ToArray(), transferDataList)

let syncChangeJobOrder jobDB =
    jobDB.SyncEvents.FullUpdate.Trigger(getUIData jobDB)

let jobOrderChanged jobDB = syncChangeJobOrder jobDB


///<summary>Called to add a job to the jobdb</summary>
///<param name="makeJob"> A function that takes an id and returns a job that will be run to create the job. This allows for a job to contain its own id. if the id is wunwanted just return a job as usual.</param>
let private addJob jobDB sourceID makeJob transData =
    Lgdebug "[Access] Adding job to source wih id: {@srcID}  "sourceID
    let { FreeTokens = freeTokens; Sources = sources; JobList = jobList } = jobDB
    let source = sources.[sourceID]
    let id = JobList.addJob jobList makeJob

    jobDB.JobOrder.Add(sourceID)

    let job = jobList.[id]

    source.Jobs.Add(job)

    SourceList.getNextToken freeTokens source job

    TransferDataList.setAndSync jobDB.TransferDataList jobDB.SyncEvents id (transData id)
    //run various actions that should trigger on adding a job

    jobOrderChanged jobDB
    //return
    Lgdebugf "Added job jobID %i" id
    id

///Removes the job from the runningList and moves it to finished. 
///Also returns tokens and triggers an attempt to distribute those tokens to other jobs
let MakeJobFinished jobDB sourceID jobID =
    let { FreeTokens = freeTokens; FinishedJobs = finishedList; Sources = sources; JobList = jobList;
          RunningJobs = runningJobs } =
        jobDB

    let job = jobList.[jobID]

    let finish () = finishedList.Add(jobID)


    if runningJobs.Remove(jobID) then
        finish ()
    else
        try
            removeWaitingJob jobDB (sources.[sourceID]) sourceID jobID
            finish ()
        with a -> Lgerror2 "Job {@ID} failed to be removed. Reason: {@reason}" job a
    job.TakenTokens
    |> List.iter (fun id -> TokenList.returnToken freeTokens id)

    job.TakenTokens
    |> List.rev
    |> List.iter (fun id ->
        freeTokens.[id]
        |> SourceList.attmeptIssuingToken sources)

    tryRunJobs jobDB
    jobOrderChanged jobDB
    Lgdebug "Removed job {@id" jobID

///Makes the upjob higher up the order of jobs than the downjob
///This is done by either reordering source in the joborder or reording jobs within a source
///If the jobs are both from the same source it is reordered in the source, else the sources are reordered in the JobOrder.
let private switch jobDB (downJob: JobID) upJob =
    let { Sources = sources; JobOrder = jobOrder } = jobDB
    let job1Source = jobDB.JobList.[downJob].SourceID
    let job2Source = jobDB.JobList.[upJob].SourceID


    let pos1 =
        sources.[job1Source].Jobs.FindIndex(Predicate(fun x -> x.ID = downJob))

    let pos2 =
        sources.[job2Source].Jobs.FindIndex(Predicate(fun x -> x.ID = upJob))

    //if the sources are the same we move within a SourceList
    if job1Source=job2Source then
    
        if pos1 = (pos2 - 1) || pos1 - 1 = pos2 then
            let list = sources.[job1Source]
            //this siwtches the jobs tokens
            let downJobTokens = list.Jobs.[pos1].TakenTokens
            list.Jobs.[pos1].TakenTokens <- list.Jobs.[pos2].TakenTokens
            list.Jobs.[pos2].TakenTokens <- downJobTokens
            //this switches the jobs themselves
            if pos1 = (pos2 - 1) then
                list.Jobs.Reverse(pos1, 2)
            else if (pos1 - 1) = pos2 then
                list.Jobs.Reverse(pos2, 2)
            else
                Lgerror2
                    "Jobs requested to be switched are not adjacent. At positions {@pos1} and {@pos2} This should not be."
                    pos1
                    pos2
        else
            Lgwarn2
                "Jobs requested to be switched are in the same source but not adjacent. At positions {@pos1} and {@pos2} This should not be. Jobs have not been switched"
                pos1
                pos2
    //If the sources are differnet we change the position of SourceIDs in the JobOrder
    else
    
        //Essentially what we do is count instances of a source in the jobOrder untill we get to the instance that is the same depth
        //in the jobOrder as the job we are switching is in the source itself
        //eg: jobOrder:[a,b,a,a,b,a,b] a:[j1,j2,j4,j5,] b:[j3,j6,j7]
        //we want to switch job j6 and j4.      ^             ^
        //that would be the 2nd "b job" and 3rd "a job"
        //So we fnd the index in the JobOrder of the 2nd "b" instance and the 3rd "a" instance  
        let mutable index1 = 0
        let mutable count1 = 0
        let mutable index2 = 0
        let mutable count2 = 0
        
        //We count instances untill we get to the one we want then we set the index to that.
        for id in 0 .. jobOrder.Count - 1 do
            let testSource= jobOrder.[id]
            if testSource = job1Source then
                count1 <- count1 + 1
                if count1 = pos1 + 1 then index1 <- id
            else if testSource = job2Source then
                count2 <- count2 + 1
                if count2 = pos2 + 1 then index2 <- id
        //Switch the indexs of the sources.
        jobOrder.[index1] <- job2Source
        jobOrder.[index2] <- job1Source

    jobOrderChanged jobDB

///A job being avialable means that anything it has to wait for to complete has completed. 
///Currently this means the file it is attempting to move has been written fully. 
///Only available jobs can be run.
let makeJobAvailable jobDB id =
    jobDB.JobList.[id].Available <- true
    tryrunJob jobDB id


type JobListAccess(jobList, req:RequestHandler) =
    let d1 (f: 'a -> 'c) = req.doSyncReq f
    member this.GetJob id = d1 (JobList.getJob jobList) id
    member this.RemoveJob id = d1 (JobList.removeJob jobList) id

type TransDataAccess(transDataList: TransferDataList, req:RequestHandler, syncer) =
    let d (f: 'a -> 'c) = req.doSyncReq f

    member this.SetAndSync jobID data =
        d (TransferDataList.setAndSync transDataList syncer jobID) data

    member this.Get id =
        d (TransferDataList.get transDataList) id

    member this.Remove id =
        d (TransferDataList.remove transDataList) id
///Allows for access to the job database. All access is single threaded.
///Each function call will have to wait for any calls before it to complete before being run.
type DBAccess(jobDB: JobDataBase) =    
    let handler:RequestHandler= MessageRequestHandler() :>RequestHandler

    let d (f:'a->'c)= handler.doSyncReq f
    let a x = x :> Object
    member this.JobListAccess = JobListAccess(jobDB.JobList, handler)

    member this.TransDataAccess =
        TransDataAccess( jobDB.TransferDataList,handler, jobDB.SyncEvents)

    member this.GetUIData() = d getUIData jobDB
    member this.MakeJobFinished id = d (MakeJobFinished jobDB) id
    member this.MakeJobAvailable id = d (makeJobAvailable jobDB) id

    member this.CancelJob id =
        d (fun id -> (JobList.getJob jobDB.JobList id).CancelToken.Cancel()) id

    member this.AddJob sourceID makeJob transData =
        d (addJob jobDB sourceID makeJob) transData

    member this.SwitchJobs downJob upJob = d (switch jobDB downJob) upJob
