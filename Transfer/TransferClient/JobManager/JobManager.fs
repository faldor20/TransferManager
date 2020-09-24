namespace TransferClient

open System
open System.Collections.Generic
open SharedFs.SharedTypes
open TransferClient.IO.Types
open TransferClient.JobManager
module JobManager =

    

  

    
    type TransferDataList =Dictionary<JobID, SharedFs.SharedTypes.TransferData>

 
    open System.Linq
 
    ///SoureOrder is a list of the sources that want the token. each time a token is issued the issuing sources id is put at the end
   
    type RunningJobs= List<JobID>
    type FinishedJobs=List<JobID>

    type UIData= {
        TransferDataList:TransferDataList
        Jobs:(JobID*HierarchyLocation)array
    }
    type JobDataBase={
        JobOrder:JobOrder;
        mutable FreeTokens:TokenList
        RunningJobs:RunningJobs
        FinishedJobs:FinishedJobs
        mutable Sources:SourceList
        TransferDataList:TransferDataList
        JobList:JobList
        mutable RunJob:ScheduleID->JobID->unit
    }
    let JobDataBase runJob={
        JobOrder=JobOrder();
        FreeTokens=TokenList()
        RunningJobs=RunningJobs()
        FinishedJobs=FinishedJobs()
        Sources=SourceList()
        TransferDataList=TransferDataList()
        JobList=JobList()
        RunJob=runJob
    }
    let tryRunJobs (jobDB:JobDataBase) processJob =
        let jobsTorun=
            JobOrder.takeAvailableJobs jobDB.JobOrder jobDB.Sources
        jobsTorun|>Seq.iter(fun (x,i)->
        jobDB.RunJob x i 
        )

        


     //there will be two main loops managing the jobs. One loop gives new tokens the other runs new jobs. 
    //these jobs should relaly only be run occasionaly. The reasons they should be run are listed below
    //A new job being added should recursiveley run get next token untill it has got all tokens and can be run or has not and can be left
    //A job being removed should go through each token being put back in reverse order and attempt to give to a job
    //those should be the only two times action needs to be taken
    ///mkeJob is  function that takes an id and returns a job. this allows for a job to contain its own id
    let addJob (freeTokens:TokenList)  (sources:SourceList) (jobList:JobList) sourceID makeJob=
        let source=sources.[sourceID]
        let id=JobList.addJob jobList makeJob
        let job= jobList.[id]
        source.Jobs.Add(job)
        SourceList.getNextToken freeTokens source job
        id
        //TODO: trigger an attempt to run any job with all its tokens
    ///removes the job from the runningList and moves it to finished. Also returns tokens and trigger an attempt to distribute those tokens
    let removeJob {FreeTokens=freeTokens; FinishedJobs=finishedList; Sources=sources; JobList=jobList; RunningJobs=runningJobs}  sourceID jobID=
        let job= jobList.[jobID]
        
        if runningJobs.Remove(jobID) then
            finishedList.Add(jobID)
            job.TakenTokens
            |> List.iter (fun id -> TokenList.returnToken freeTokens id)

            job.TakenTokens
            |>List.rev
            |>List.iter(fun id -> freeTokens.[id]|>SourceList.attmeptIssuingToken sources)
        else Logging.errorf "Job %A failed to be removed. something must have gone wrong" job

        //TODO: trigger an attempt to run any job with all its tokensp

    ///makes the upjob higher up the order of jobs than the downjob
    /// pos is the scheduleid and index within the source of a particular job
    let switch (sources:SourceList) (jobOrder:JobOrder) (downJob:ScheduleID*int) upJob =
       
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

    let getUIData ({JobOrder=jobOrder;JobList=jobList;Sources=sources; FinishedJobs=finishedJobs; RunningJobs=runningJobs; TransferDataList=transferDataList; }:JobDataBase)=
                let orderdIDs= 
                    JobOrder.countUp jobOrder
                    |>Seq.map(fun (id,index)-> sources.[id].Jobs.[index].ID,sources.[id].RequiredTokens)
                //this only works on jobs that have all the tokens they need.
                //this is becuase the ScheduleIDList can be usefull in filtering at the ui end
                let convertToOut (li:List<JobID>)=
                    li.Select(fun x->x,jobList.[x].TakenTokens)

                let jobIDs=(convertToOut finishedJobs).Concat(convertToOut runningJobs).Concat( orderdIDs)
                {Jobs=jobIDs.ToArray();TransferDataList=transferDataList}
    type Access={
        GetJob:JobID->JobItem
        TransDataAccess:TransferDataList.Acess
        GetUIData:unit->UIData
        SwitchJobs: (ScheduleID*int)->(ScheduleID*int)->unit
        RemoveJob:ScheduleID->JobID->unit
        AddJob: ScheduleID->(int->JobItem)->JobID
    }
    let access (jobDB:JobDataBase)={
        
        GetJob=JobList.getJob jobDB.JobList 
        TransDataAccess=TransferDataList.acessFuncs jobDB.TransferDataList
        GetUIData=(fun ()->(getUIData jobDB));
        SwitchJobs=switch jobDB.Sources jobDB.JobOrder;
        RemoveJob = removeJob jobDB;
        AddJob=addJob jobDB.FreeTokens jobDB.Sources jobDB.JobList
    }
                