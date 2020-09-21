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