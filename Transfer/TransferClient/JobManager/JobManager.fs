namespace TransferClient.JobManager

open System
open System.Collections.Generic
open SharedFs.SharedTypes
open TransferClient.IO.Types
open SharedFs.SharedTypes
open TransferClient
module Main =


 
    open System.Linq
 
    ///SoureOrder is a list of the sources that want the token. each time a token is issued the issuing sources id is put at the end
   
    type RunningJobs= List<JobID>
    type FinishedJobs=List<JobID>
    type JobDataBase={
        JobOrder:JobOrder;
        mutable FreeTokens:TokenList
        RunningJobs:RunningJobs
        FinishedJobs:FinishedJobs
        mutable Sources:SourceList
        TransferDataList:TransferDataList
        JobList:JobList
        mutable RunJob:ScheduleID->JobID->Async<unit>
        UIData: ref<UIData>
        
    }
    let JobDataBase runJob mapping ={
        JobOrder=JobOrder();
        FreeTokens=TokenList()
        RunningJobs=RunningJobs()
        FinishedJobs=FinishedJobs()
        Sources=SourceList()
        TransferDataList=TransferDataList()
        JobList=JobList()
        RunJob=runJob
        UIData=ref <|UIData mapping
    }
    let private runJob jobDB jobsToRun=
        Logging.debugf "Running jobs%A"jobsToRun
        lock jobDB.RunningJobs (fun ()->
        jobsToRun|>List.iter(fun (x,i)->
        jobDB.RunningJobs.Add(i)
        jobDB.RunJob x i |>Async.Start
        ))
    ///trys to run the job. This will remove the job from it source, remove the first instance of the source from the jobOrder
    /// and add it to runningjobsList
    ///Can run a job out of order... but if only called when a job ebcaome avaiable order should be preserved
    let tryrunJob (jobDB:JobDataBase) id=
        lock jobDB.Sources (fun ()->
        Logging.debugf "Trying to run job %i"id
        let job=JobList.getJob jobDB.JobList id
        if job.TakenTokens.Length>1 then
            let sourceID=List.last job.TakenTokens
            let source=jobDB.Sources.[sourceID]
            
            if source.RequiredTokens=job.TakenTokens then
                lock jobDB.JobOrder (fun ()->
                match jobDB.JobOrder.Remove(sourceID) with
                    |true->()
                    |false->Logging.errorf "Tried to remove job %i from source %A that should have been there but wasn't" id sourceID
                )
                //this removes the job from the source list
                lock source.Jobs (fun ()->
                let i=source.Jobs.FindIndex (Predicate( fun x->x.ID=id))
                match  i with
                    |(-1)->Logging.errorf "Tried to remove job %i from source %A that should have been there but wasn't" id sourceID
                    |a->source.Jobs.RemoveAt a
                )
                Logging.debugf "Running Specifically job %i from source %A "id sourceID 
                runJob jobDB [(sourceID,id)]
               )


    let tryRunJobs (jobDB:JobDataBase)  =
        lock jobDB.Sources (fun ()-> 
        let jobsTorun=
            JobOrder.takeAvailableJobs jobDB.JobOrder jobDB.Sources
        runJob jobDB jobsTorun
        )

        
        


    let getUIData ({JobOrder=jobOrder;JobList=jobList;Sources=sources; FinishedJobs=finishedJobs; RunningJobs=runningJobs; TransferDataList=transferDataList; UIData=uIData; }:JobDataBase)=
                lock uIData (fun()->
                let orderdIDs= 
                    JobOrder.countUp jobOrder
                    |>Seq.map(fun (id,index)-> {JobID= sources.[id].Jobs.[index].ID; RequiredTokens= sources.[id].RequiredTokens.ToArray()})
                //this only works on jobs that have all the tokens they need.
                //this is becuase the ScheduleIDList can be usefull in filtering at the ui end
                //we can use taken tokens becuase running and finished jobs will have all their tokens,
                // it is easier than  getting the requiretokens from the source for each token
                let convertToOut (li:List<JobID>)=
                    li.Select(fun x->{JobID=x;RequiredTokens=jobList.[x].TakenTokens.ToArray()})
                //TODO: t
                let jobIDs=(convertToOut finishedJobs).Concat(convertToOut runningJobs).Concat( orderdIDs).Reverse()
                
                {  Jobs=jobIDs.ToArray();NeedsSyncing=true; UIData.Mapping= uIData.Value.Mapping ;UIData.TransferDataList=transferDataList }) 
    let syncChangeJobOrder jobDB =
        jobDB.UIData:= getUIData jobDB
        
    let jobOrderChanged jobDB=
        syncChangeJobOrder jobDB
    
     //there will be two main loops managing the jobs. One loop gives new tokens the other runs new jobs. 
    //these jobs should relaly only be run occasionaly. The reasons they should be run are listed below
    //A new job being added should recursiveley run get next token untill it has got all tokens and can be run or has not and can be left
    //A job being removed should go through each token being put back in reverse order and attempt to give to a job
    //those should be the only two times action needs to be taken
    ///mkeJob is  function that takes an id and returns a job. this allows for a job to contain its own id
    let addJob  jobDB sourceID  makeJob transData=
        
        let {FreeTokens=freeTokens;  Sources=sources; JobList=jobList; }=jobDB
        let source=sources.[sourceID]
        let id=JobList.addJob jobList makeJob
        lock jobDB.JobOrder (fun()->
        jobDB.JobOrder.Add(sourceID)
        )
        let job= jobList.[id]
        lock source.Jobs (fun ()->
        source.Jobs.Add(job)
        )
        SourceList.getNextToken freeTokens source job

        TransferDataList.setAndSync jobDB.TransferDataList jobDB.UIData id (transData id)
        //run various actions that should trigger on Add
        //TODO: test if this can be deleted. jobs most likel will not be able to be run after adding becuase teyneed to be confirmed as available
        
        //tryRunJobs jobDB 
        jobOrderChanged jobDB
        //return
        Logging.debugf "Added job jobID %i"id
        id
        
    ///removes the job from the runningList and moves it to finished. Also returns tokens and trigger an attempt to distribute those tokens
    let MakeJobFinished jobDB  sourceID jobID=
        let {FreeTokens=freeTokens; FinishedJobs=finishedList; Sources=sources; JobList=jobList; RunningJobs=runningJobs} =jobDB
        let job= jobList.[jobID]
        lock jobDB.FinishedJobs (fun ()->
        if runningJobs.Remove(jobID) then
            finishedList.Add(jobID)
            job.TakenTokens
            |> List.iter (fun id -> TokenList.returnToken freeTokens id)

            job.TakenTokens
            |>List.rev
            |>List.iter(fun id -> freeTokens.[id]|>SourceList.attmeptIssuingToken sources)
        else Logging.errorf "Job %A failed to be removed. something must have gone wrong" job
        )
        tryRunJobs jobDB 
        jobOrderChanged jobDB
        Logging.debugf"Removed job %i"jobID
        //TODO: trigger an attempt to run any job with all its tokensp
    ///makes the upjob higher up the order of jobs than the downjob
    let switch  jobDB (downJob:JobID) upJob =
        let {Sources=sources ;JobOrder=jobOrder} =jobDB
        let job1Source=jobDB.JobList.[downJob].SourceID
        let job2Source=jobDB.JobList.[upJob].SourceID
        lock sources.[job1Source] (fun ()->
        lock sources.[job2Source] (fun ()->
        let pos1=sources.[job1Source].Jobs.FindIndex(Predicate(fun x->x.ID=downJob))
        let pos2=sources.[job2Source].Jobs.FindIndex(Predicate(fun x->x.ID=upJob))
        match (job1Source,downJob),(job2Source,upJob) with
        //if the sources are the same we move within a souceList
        | ((source1,id1),(source2,id2)) when source1=source2->
                let list= sources.[source1]
                //this siwtches the jobs tokens
                let downJobTokens =list.Jobs.[pos1].TakenTokens
                list.Jobs.[pos1].TakenTokens<-list.Jobs.[pos2].TakenTokens
                list.Jobs.[pos2].TakenTokens<-downJobTokens
                //this switches the jobs themselves
                if pos1=(pos2-1)then list.Jobs.Reverse(pos1,2)
                else if (pos1-1)=pos2 then list.Jobs.Reverse(pos2,2) 
                else Logging.errorf "Jobs requested to be switched are not adjacent. At positions %i and %i This should not be."pos1 pos2

            //if the sources are differnet we change the position of scheduleids in the jobOrder
        |(source1,id1),(source2,id2)  ->
            lock jobOrder (fun ()->

                //if the sources don't match we need to leve them as is and swap the positions of the scheduleID's in the joborder 
                            
                //Essentially what we do is count instances of each source in the jobOrder untill we get to the source that is the same depth in the jobOrder as the job we are switching is in the source itself
                //eg: jobOrder:[a,b,a,a,b,a,b] a:[j1,j2,j4,j5,] b:[j3,j6,j7] 
                //we want to switch job j6 and j4.      ^             ^
                // that would be the 2nd "b job" and 3rd "a job"
                //so we fnd the index in the joborder of the 3rd a instance and 2nd b instance
                let mutable index1=0
                let mutable count1=0
                let mutable index2=0
                let mutable count2=0
                for id in {0..jobOrder.Count}do
                    if jobOrder.[id]=job1Source then
                        count1<-count1+1
                        if count1=pos1+1 then
                            index1<-id
                    else if jobOrder.[id]=job2Source then
                        count2<-count2+1
                        if count2=pos2+1 then
                            index2<-id

                jobOrder.[index1] <- job2Source
                jobOrder.[index2] <- job1Source
            )
        jobOrderChanged jobDB
        ))

    type Access={
        GetJob:JobID->JobItem
        TransDataAccess:TransferDataList.Acess
        GetUIData:unit->UIData
        SwitchJobs: (int)->(int)->unit
        MakeJobFinished:ScheduleID->JobID->unit
        AddJob: (ScheduleID)->(int->JobItem)->(int->TransferData)->JobID
        makeJobAvailable: JobID->unit
    }
    let access ( jobDB: JobDataBase)={
        
        GetJob=JobList.getJob jobDB.JobList 
        TransDataAccess=TransferDataList.acessFuncs jobDB.TransferDataList jobDB.UIData
        GetUIData=(fun ()->(getUIData jobDB));
        SwitchJobs=switch jobDB;
        MakeJobFinished = MakeJobFinished jobDB;
        AddJob=addJob jobDB
        //TODO move to won function
        makeJobAvailable=(fun id->

            jobDB.JobList.[id].Available<-true
            //TODO: make this only try to run the one job you just got given
            tryrunJob jobDB id)
    }
                