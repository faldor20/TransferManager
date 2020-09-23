namespace TransferClient

open System
open System.Collections.Generic
open SharedFs.SharedTypes
open TransferClient.IO.Types

module JobManager =
    type ScheduleID = int
   
    type JobID = int
    type Job = Async<TransferResult*bool>

    type JobItem =
        { Job: Job
          mutable TakenTokens: ScheduleID list }
    ///A list of ScheduleIDs represetnig a specific place within the jobHeirachy with the highest level id at the beginning and the deepest at the end
    type HierarchyLocation = ScheduleID list
    /// A list of the jobs that came from each schedule ID ordered by their hieght in the hierache and distance down the varisous lists end is highest beginning is lowest
    /// this list is used for reording jobs , because reording should be doable per source
    type JobOrder= Dictionary<ScheduleID, JobID list>
    /// a list where each value is  a level in the hierarchy, each level contains a list of Heiracheylocations
    /// this is so that the jobHeirachy can be iterated bottom up during a shuffleup
    /// eg:       1             index=2
    ///    1,1       ;     1,2  index=1
    /// 1,1,1; 1,1,2  ; 1,2,1    index=0
    type HierarchyOrder = HierarchyLocation list list
    type FinishedJobs= list<JobID>

    type FreeTokens = Dictionary<ScheduleID, int>
    type JobList = Dictionary<JobID, JobItem>
    type TransferDataList =Dictionary<JobID, SharedFs.SharedTypes.TransferData>

    type JobHierarchy = Dictionary<HierarchyLocation, JobID list>
    ///this record contains only data needing to be sent to the clientManager and by extension the UI
    
    let lockedFunc locObj func=
        lock(locObj) (fun()->func )
    type JobDataBase={
        mutable TransferDataList:TransferDataList
        mutable JobHierarchy:JobHierarchy
        mutable HierarchyOrder: HierarchyOrder
        mutable JobList:JobList
        mutable FinishedJobs:FinishedJobs
        mutable FreeTokens:FreeTokens
    }
    let JobDataBase()={
         FinishedJobs=List.Empty
         TransferDataList=TransferDataList()
         JobHierarchy=JobHierarchy()
         HierarchyOrder= HierarchyOrder.Empty
         JobList=JobList()
         FreeTokens=FreeTokens()
    }
    ///this record contains only data needing to be sent to the clientManager and by extension the UI
    type UIData = {
        mutable TransferDataList:TransferDataList
        mutable JobHierarchy:JobHierarchy
        mutable FinishedJobs:FinishedJobs
    }
    let UIData (jobDB:JobDataBase):UIData={TransferDataList=jobDB.TransferDataList;JobHierarchy=jobDB.JobHierarchy;FinishedJobs=jobDB.FinishedJobs} 
    module FreeTokensF =
        let takeToken  id (tokenDB: FreeTokens)=
            let freeTokens = tokenDB.[id]

            let newTokens, outp =
                match freeTokens with
                | a when a > 0 -> freeTokens - 1, Some id
                | a when a = 0 -> 0, None
                | a when a < 0 -> failwithf "FreeTokens for tokenId %A under 0 this should never ever happen" id

            tokenDB.[id] <- newTokens
            outp

        let returnToken (tokenDB: FreeTokens) id = tokenDB.[id] <- tokenDB.[id] + 1
        let setFreeTokens (tokenDB: FreeTokens) id amount = tokenDB.[id] <- amount
        type FreeTokensAcessFuncs={
            SetFreeTokens:ScheduleID ->int ->unit
        }
        let FreeTokensAcessFuncs tokenDB={
            SetFreeTokens =setFreeTokens tokenDB
        }
    module JobListF =
        let addJob (list: JobList) (jobItem:int->JobItem) =
            let id = list.Count
            list.[id] <- (jobItem id)
            id
        ///Returns a refernce to the job whos id is given
        let getJob (list: JobList) id = list.[id]
        ///Returns a refernce to the job whos id is given
        let setJob (list: JobList) id = list.[id]
        ///Returns a refernce to the job whos id is given
        let removeJob (list: JobList) id= list.Remove id
        let giveToken  id token (list: JobList)= list.[id].TakenTokens<-token::list.[id].TakenTokens
        type JobListAcessFuncs =
            {
                GetJob:JobID->JobItem
                RemoveJob:JobID->bool
                AddJob:(int->JobItem) ->int
            }

        let JobListAcessFuncs jobList=
            {
                GetJob=getJob jobList
                RemoveJob=lockedFunc jobList (removeJob jobList)
                AddJob= lockedFunc jobList (addJob jobList)
            }
    module TransferDataListF=
        let set (transferDataList:TransferDataList) jobID data=
            transferDataList.[jobID]<-data
        let get (transferDataList:TransferDataList) jobID=
            transferDataList.[jobID]
        let remove (transferDataList:TransferDataList) jobID=
            transferDataList.Remove(jobID)
        type TransDataAcessFuncs =
            {
                Set:JobID ->TransferData->unit
                Get:JobID->TransferData
                Remove:JobID ->bool
            }

        let acessFuncs transDataList=
            {
                Set=set transDataList
                Get= get transDataList 
                Remove= remove transDataList
            }
    module JobHierarchyF =
        let addJob  jobId location (hierarchy: JobHierarchy)=
            let list = hierarchy.[location]
            hierarchy.[location] <- jobId :: list

        let private removeJob (jobId: JobID) location (hierarchy: JobHierarchy) =
            let list = hierarchy.[location]
            hierarchy.[location] <- (list |> List.except [ jobId ])
        ///removes a job from the heirarchey and returns its taken tokens
        ///Doesn't actually remove the tokens from the job
        let public deleteJob   (jobDB:JobDataBase) jobId location=
            jobDB.JobHierarchy|>removeJob  jobId location
            (JobListF.getJob jobDB.JobList jobId).TakenTokens
            |> List.iter (fun token -> FreeTokensF.returnToken jobDB.FreeTokens token)
            JobListF.removeJob jobDB.JobList jobId
        type ShiftResult=
        |noJobs=0
        |noFreeTokens=1
        |worked=2
        ///Returns whether or not the job was moved
        let private tryShiftUpHierarchy (jobDB:JobDataBase)  (location:HierarchyLocation)=
            if jobDB.JobHierarchy.[location].Length>0 then 
                let jobId=jobDB.JobHierarchy.[location].Head
                let target= location.[location.Length-2]
                let newLocation= location|>List.take (location.Length-1) 
                match jobDB.FreeTokens|>FreeTokensF.takeToken target  with
                    |Some x->
                        jobDB.JobList|> JobListF.giveToken jobId target   //Add new token to jobs taken list
                        jobDB.JobHierarchy|>removeJob jobId location//remove job from current level
                        jobDB.JobHierarchy|>addJob jobId newLocation //add job to above level
                        ShiftResult.worked
                    |None->ShiftResult.noFreeTokens
            else ShiftResult.noJobs
        ///swaps two items in the list the first one being at index i and second at i+1
        let private switchItems list i=
            let before,rest=list|>List.splitAt i
            let after=rest|>List.skip 2
            List.concat [before;[list.[i+1]];[list.[i]];after;]
        (* let ``move up level taking teh place of the most recent item`` aboveList=
            aboveList.head *)

        //TODO: This currently does not allow moving up if the heirachy above is full
        let tryMoveUp  (jobDB:JobDataBase)  jobId (location:HierarchyLocation)=
            let list=jobDB.JobHierarchy.[location]
            let index=list|>List.findIndex ((=)jobId)
            match index with
            //This moves the job up a level if it is at the end of its current levels list
            |i when i=(list.Length-1)->
                if location.Length>1 then 
                    tryShiftUpHierarchy jobDB  location 
                else ShiftResult.noJobs
            |i->
                jobDB.JobHierarchy.[location]<- switchItems list i
                ShiftResult.worked
        ///trys to move all jobs in the jobHierarchy up the hierarchy if possible. 
        ///Operates on each position in the hierarchy based on th locations given in the hierarchy order.
        ///returns a list of all locations that had a job moved and a bool saying if there are free tokens(justifying running shuffelup again) 
        let shuffelUp  (jobDB:JobDataBase)=
            let rec iter (locations) moved=
                    match locations with
                    |head::tail->
                        match tryShiftUpHierarchy jobDB head with
                        |ShiftResult.worked-> iter tail (head::moved)
                        |ShiftResult.noJobs-> iter tail moved
                        |ShiftResult.noFreeTokens ->(false,moved)
                    |[]->(true,moved)
            ///takes a list of HierarchyLocations and does a shuffelup on them returns all the hierarchy locations that had a job moved out of them
            let rec doShuffel list moveResults=
                match iter list [] with
                |(true,lis) when lis.Length>1->
                    //we append the most recent result becuase we want the last location in MoveResults to be the most recently moved one 
                    doShuffel lis (moveResults|>List.append lis) 
                |_->moveResults
            let newOrder=jobDB.HierarchyOrder|>List.map (fun level -> 
                let movedLocations=[]|>doShuffel level
                
                //the movedLocations is appended becuase we want the last irem to be the most recently moved.
                //(we can't prepend and forgo the reverse beuase that would loose the existing order of the level)
                //we reverse the list because distinct discards all later versions of a location. This means that the results of doShuffle will be kept and any other istances of the location will be removed.
                level
                    |>List.append movedLocations
                    |>List.rev
                    |>List.distinct
                    |>List.rev )
            jobDB.HierarchyOrder<-newOrder
        let getTopJob (jobDB:JobDataBase)=
            try
            let location=
                jobDB.HierarchyOrder |>List.last
                    |>List.head
            let jobID=jobDB.JobHierarchy.[location]|>List.last
            Some(jobID,location)
            

                
            with|ArgumentException ->None
                

        type JobHierarchyAccess =
            {
                AddJob:JobID->HierarchyLocation->unit
                DeleteJob:JobID->HierarchyLocation->bool
                TryMoveUp:JobID->HierarchyLocation->ShiftResult
                ShuffelUp: unit->unit
                GetTopJob:unit ->(JobID*HierarchyLocation) option
            }
        let JobHierarchyAccess (jobDB:JobDataBase)=
            {
                DeleteJob=lockedFunc jobDB (deleteJob jobDB)
                TryMoveUp= lockedFunc jobDB (tryMoveUp jobDB )
                ShuffelUp= fun ()-> lockedFunc jobDB (shuffelUp jobDB)
                AddJob=fun x y->(lockedFunc jobDB (jobDB.JobHierarchy|>addJob x y) )
                GetTopJob=fun()-> getTopJob jobDB
            }

    type JobDBAccess={
        FinishedJobs: {|Add:JobID-> unit|}
        TransferDataList:TransferDataListF.TransDataAcessFuncs
        Hierarchy: JobHierarchyF.JobHierarchyAccess
        JobList:JobListF.JobListAcessFuncs
        FreeTokens:FreeTokensF.FreeTokensAcessFuncs
    }
    let JobDBAccess (jobDB:JobDataBase)=
        {   FinishedJobs={|Add=(fun (jobID:JobID)-> jobDB.FinishedJobs<- (jobID::jobDB.FinishedJobs))|}
            TransferDataList=TransferDataListF.acessFuncs jobDB.TransferDataList
            Hierarchy= JobHierarchyF.JobHierarchyAccess jobDB
            JobList=JobListF.JobListAcessFuncs jobDB.JobList
            FreeTokens=FreeTokensF.FreeTokensAcessFuncs jobDB.FreeTokens
        }



module simpleDB=
    open JobManager
    open System.Linq
    //contains one schedule id for each job in each jobsource.
    // source a=[job1,job2] b=[job1] jobOrder=[(a);(b);(a)]
    type JobOrder= (ScheduleID)ResizeArray
    type JobItem =
        { Job: Job
          ID:JobID
          mutable TakenTokens: ScheduleID list }
    
    ///SoureOrder is a list of the sources that want the token. each time a token is issued the issuing sources id is put at the end
    type TokenStore={
        Token:ScheduleID;
        mutable Remaining:int;
        mutable SourceOrder: ScheduleID list 
    }
    type FreeTokens = Dictionary<ScheduleID, TokenStore>
    type RunningJobs= List<JobID>
    type FinishedJobs=List<JobID>
    ///required tokens is last reuired to firstrequired
    /// Rules:
    /// A job with a lower number of tokens will allways sit below one with a higher number
    /// Jobs will allways be removed from the list in the order they were added(assuming no user swap requests)
    type Source= {Jobs:ResizeArray<JobItem>;RequiredTokens:ScheduleID list}
    type Sources= Dictionary<ScheduleID,Source>
    type UIData= {
        TransferDataList:TransferDataList
        Jobs:(JobID*HierarchyLocation)array
    }
    let countBefore (jobOrder:JobOrder) countItem index=
            let mutable amount=0
            for i in [0..index-1] do
                if jobOrder.[i]=countItem then amount<-amount+1
            amount
    let countUp (jobOrder:IEnumerable<'a>)=
        let counts=Dictionary<'a,int>()
        jobOrder.Select(fun job->
            if not (counts.ContainsKey(job)) then counts.[job]<-0
            else counts.[job]<-counts.[job]+1
            (job,counts.[job])
        ).ToArray()
        
    module FreeTokensF =
        let private takeToken'  tokenSource=
            lock tokenSource (fun ()->
                let newTokens, outp =
                    match tokenSource.Remaining with
                    | a when a > 0 -> tokenSource.Remaining - 1, Some tokenSource.Token
                    | a when a = 0 -> 0, None
                    | a when a < 0 -> failwithf "FreeTokens for tokenId %A under 0 this should never ever happen" id

                tokenSource.Remaining <- newTokens
                outp)
        let takeToken  id (tokenDB: FreeTokens)=
            takeToken' tokenDB.[id]
        
        let returnToken (tokenDB: FreeTokens) id = tokenDB.[id].Remaining <- tokenDB.[id].Remaining + 1
        let setFreeTokens (tokenDB: FreeTokens) id amount = tokenDB.[id] <- amount
        let rec attmeptIssuingToken  (sources: Sources) (tokenSource:TokenStore)=
            ///This loop iterates jobs untill one is found that the token can be inserted into. At which point it returns true. if none is found it returns false
            let rec jobLoop (source:Source) ID i=
                if i=source.Jobs.Count then false 
                else
                    let job= source.Jobs.[i]
                    let nextToken=source.RequiredTokens|>List.except job.TakenTokens|>List.last
                    if nextToken= tokenSource.Token then
                        match takeToken' tokenSource with
                        |Some token->
                            job.TakenTokens<- token::job.TakenTokens
                            //This removes our item and then adds it at the end
                            tokenSource.SourceOrder <-(tokenSource.SourceOrder|>List.except[ID])@[ID]
                            //we then run it again just incase there are some tokens left
                            true
                        |None->
                            Logging.errorf "Something has gone wrong an attempt was made to issue a token but it failed"
                            true
                    else jobLoop source ID i
            ///loops until the jobloop returns true, then runs the main function again. this is incase multiple tokens were added at once
            let rec iter (sourceIDs)=
                match sourceIDs with
                | head::tail-> 
                    if jobLoop sources.[head] head  0 then tokenSource|>attmeptIssuingToken  sources
                    else iter tail
                |[]->()
            if tokenSource.Remaining>0 then
                iter tokenSource.SourceOrder
                    
    /// pos is the scheduleid and index within the source of a particular job
    let switch (sources:Sources) (jobOrder:JobOrder) (downJob:ScheduleID*int) upJob =
       
        match downJob,upJob with
        //if the sources are the same we move within a souceList
        | ((source1,i1),(source2,i2)) when source1=source2->
            let pos1=countBefore jobOrder source1 i1
            let pos2=countBefore jobOrder source1 i1
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

    let takeAvailableJobs (jobOrder:JobOrder) (sources:Sources)=
        let indexed=countUp jobOrder
        let jobsToRun=
            seq{
            for (jobSource,i) in indexed do
                let job=sources.[jobSource].Jobs.[i]
                if job.TakenTokens = sources.[jobSource].RequiredTokens then
                    yield (jobSource,i)
            }
        
        
        jobsToRun|>Seq.iter(fun (id,index)->
            match jobOrder.Remove(id) with
            |true->()
            |false->Logging.errorf "Tried to remove a job that should have been there but wasn't"
            //this removes the job from the source list
            sources.[id].Jobs.RemoveAt(index)
            )
        

    ///this should
    let rec getNextToken (freeTokens:FreeTokens)  (source:Source) (job:JobItem) =
        if job.TakenTokens.Length =source.RequiredTokens.Length then
            ()
        else
        //this is done very very often and so it could be made faster by precomputing the requiredToken.
            let neededToken=
                source.RequiredTokens
                |>List.except job.TakenTokens
                |>List.last
            match freeTokens|> FreeTokensF.takeToken neededToken with
            |Some token-> 
                job.TakenTokens<- token::job.TakenTokens
                getNextToken freeTokens source job
            |None->()
    ///Shold be run at regular intervals to give jobs the tokens they need to be picked up by the runner and run 
    /// this makes ordering fair because it attemps to give tokens in the same alternating order as the jobqueue
    let updateTokens  (freeTokens:FreeTokens)  (sources:Sources) =
        sources|>Seq.iter (fun source->
            for job in source.Value.Jobs do
                getNextToken freeTokens source.Value job
            
        )
    //there will be two main loops managing the jobs. One loop gives new tokens the other runs new jobs. 
    //these jobs should relaly only be run occasionaly. The reasons they should be run are listed below
    //A new job being added should recursiveley run get next token untill it has got all tokens and can be run or has not and can be left
    //A job being removed should go through each token being put back in reverse order and attempt to give to a job
    //those should be the only two times action needs to be taken
    let addJob (freeTokens:FreeTokens)  (sources:Sources)  sourceID job=
        let source=sources.[sourceID]
        source.Jobs.Add(job)
        getNextToken freeTokens 

    let removeJob (freeTokens:FreeTokens) (finishedList:FinishedJobs) (sources:Sources)  sourceID job=

        let source=sources.[sourceID]
        if source.Jobs.Remove(job) then
            job.TakenTokens
            |> List.iter (fun id -> FreeTokensF.returnToken freeTokens id)
            job.TakenTokens
            |>List.rev
            |>List.iter(fun id -> freeTokens.[id]|>FreeTokensF.attmeptIssuingToken sources)
        else Logging.errorf "Job %A failed to be removed. something must have gone wrong" job
    let getUIData (jobOrder:JobOrder) (finishedJobs:FinishedJobs) (runningJobs:RunningJobs)  (sources:Sources) (transferDataList:TransferDataList) (jobList:JobList) =
                let orderdIDs= 
                    countUp jobOrder
                    |>Seq.map(fun (id,index)-> sources.[id].Jobs.[index].ID,sources.[id].RequiredTokens)
                //this only works on jobs that have all the tokens they need.
                //this is becuase the ScheduleIDList can be usefull in filtering at the ui end
                let convertToOut (li:List<JobID>)=
                    li.Select(fun x->x,jobList.[x].TakenTokens)

                let jobIDs=(convertToOut finishedJobs).Concat(convertToOut runningJobs).Concat( orderdIDs)
                {Jobs=jobIDs.ToArray();TransferDataList=transferDataList}
                