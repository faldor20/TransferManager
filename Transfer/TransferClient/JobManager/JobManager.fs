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
    let tryRunJobs (jobDB:JobDataBase)  =
    
        let jobsTorun=
            JobOrder.takeAvailableJobs jobDB.JobOrder jobDB.Sources
        //Logging.debugf "Running jobs%A"jobsTorun
        jobsTorun|>List.iter(fun (x,i)->
        jobDB.RunningJobs.Add(i)
        jobDB.RunJob x i |>Async.Start
        )


    let getUIData ({JobOrder=jobOrder;JobList=jobList;Sources=sources; FinishedJobs=finishedJobs; RunningJobs=runningJobs; TransferDataList=transferDataList; UIData=uIData; }:JobDataBase)=
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
                let jobIDs=(convertToOut finishedJobs).Concat(convertToOut runningJobs).Concat( orderdIDs)
                
                {  Jobs=jobIDs.ToArray();NeedsSyncing=true; UIData.Mapping= uIData.Value.Mapping ;UIData.TransferDataList=uIData.Value.TransferDataList } 
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
    let addJob  jobDB sourceID makeJob=
        
        let {FreeTokens=freeTokens;  Sources=sources; JobList=jobList; }=jobDB
        let source=sources.[sourceID]
        let id=JobList.addJob jobList makeJob
        lock jobDB.JobOrder (fun ()->
        lock sources (fun ()->
        jobDB.JobOrder.Add(sourceID)
        let job= jobList.[id]
        source.Jobs.Add(job)
        SourceList.getNextToken freeTokens source job))
        //run various actions that should trigger on Add
        //TODO: test if this can be deleted. jobs most likel will not be able to be run after adding becuase teyneed to be confirmed as available
        tryRunJobs jobDB 
        jobOrderChanged jobDB
        //return
        Logging.debugf "Added job jobID %i"id
        id
        
    ///removes the job from the runningList and moves it to finished. Also returns tokens and trigger an attempt to distribute those tokens
    let removeJob jobDB  sourceID jobID=
        let {FreeTokens=freeTokens; FinishedJobs=finishedList; Sources=sources; JobList=jobList; RunningJobs=runningJobs} =jobDB
        let job= jobList.[jobID]
        
        if runningJobs.Remove(jobID) then
            finishedList.Add(jobID)
            job.TakenTokens
            |> List.iter (fun id -> TokenList.returnToken freeTokens id)

            job.TakenTokens
            |>List.rev
            |>List.iter(fun id -> freeTokens.[id]|>SourceList.attmeptIssuingToken sources)
        else Logging.errorf "Job %A failed to be removed. something must have gone wrong" job
        jobOrderChanged jobDB
        tryRunJobs jobDB 
        //TODO: trigger an attempt to run any job with all its tokensp

    ///makes the upjob higher up the order of jobs than the downjob
    /// pos is the scheduleid and index within the source of a particular job
    let switch  jobDB (downJob:ScheduleID*int) upJob =
        let {Sources=sources ;JobOrder=jobOrder} =jobDB
        match downJob,upJob with
        //if the sources are the same we move within a souceList
        | ((source1,i1),(source2,i2)) when source1=source2->
            let pos1=JobOrder.countBefore jobOrder source1 i1
            let pos2=JobOrder.countBefore jobOrder source1 i1
            let list= sources.[source1]
            //this siwtches the jobs tokens
            let downJobTokens =list.Jobs.[pos1].TakenTokens
            list.Jobs.[pos1].TakenTokens<-list.Jobs.[pos2].TakenTokens
            list.Jobs.[pos2].TakenTokens<-downJobTokens
            //this switches the jobs themselves
            if pos1=(pos2-1)then list.Jobs.Reverse(pos1,2)
            else Logging.errorf "Jobs requested to be switched are not adjacent. This should not be."
        //if the sources are differnet we change the position of scheduleids in the jobOrder
        |(down,pos1),(up,pos2)  ->
            //if the sources don't match we need to leve them as is and swap the positions of the scheduleID's in the joborder 
            //this is how we can swap order of jobs between sources
            jobOrder.[pos1] <- up
            jobOrder.[pos2] <- down
            jobOrderChanged jobDB

    type Access={
        GetJob:JobID->JobItem
        TransDataAccess:TransferDataList.Acess
        GetUIData:unit->UIData
        SwitchJobs: (ScheduleID*int)->(ScheduleID*int)->unit
        RemoveJob:ScheduleID->JobID->unit
        AddJob: ScheduleID->(int->JobItem)->JobID
        makeJobAvailable: JobID->unit
    }
    let access ( jobDB: JobDataBase)={
        
        GetJob=JobList.getJob jobDB.JobList 
        TransDataAccess=TransferDataList.acessFuncs jobDB.TransferDataList jobDB.UIData
        GetUIData=(fun ()->(getUIData jobDB));
        SwitchJobs=switch jobDB;
        RemoveJob = removeJob jobDB;
        AddJob=addJob jobDB
        //TODO move to won function
        makeJobAvailable=(fun id->

            jobDB.JobList.[id].Available<-true
            tryRunJobs jobDB)
    }
                