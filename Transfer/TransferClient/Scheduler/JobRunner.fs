namespace TransferClient.JobManager
open SharedFs.SharedTypes
open Types
open System
open FSharp.Control.Reactive
open TransferClient
open TransferClient.DataBase
open FSharp.Control
open TransferClient.IO.Types
open TransferClient.DataBase.Types
open System.Collections.Generic
module JobRunner=
    type JobItem<'a>=
        {
        Job:'a
        mutable TakenScheduleTokens:string list
        }   
    type JobList<'a>= JobItem<'a> list
    let JobItem item=
        {Job=item;TakenScheduleTokens=[""]}
    type Level<'a>={
        mutable JobList: JobList<'a>
        mutable AvailableScheduleTokens:string list
    }
    type JobDB<'a>=RecDict<string,Level<'a>,MutableData<JobList<'a>>>

    type GroupLevels<'a>=
    |MiddleLevel of Level<'a>
    |EndLevel of JobItem<'a>
    type Group<'a>=
        {
        NextGroup : GroupLevel<'a> option
        mutable JobList: JobItem<'a> list
        ScheduleTokens:List<string>
    } 
    and GroupLevel<'T>= Dictionary<string, Group<'T>>
    let private tryGetValue key (gl)=
        let getVal (dic:Dictionary<'T,'U>)=
            if dic.ContainsKey(key) then
                Some dic.[key]
            else None
        getVal (gl)
        

    let private removeJob (jobsList:'a list)=
        if jobsList.Length>=1 then 
            Some (jobsList.Head,jobsList.Tail)


        
        else None
       
(*     let getNextJob (jobGroups:Dictionary<string,'a list>) (scheduleOrder:list<string>) =
        let rec intern _jobGroups _scheduleOrder index=
            match scheduleOrder with
            | head::tail-> 
                match getJob head jobGroups with 
                |None->
                    intern _jobGroups tail (index+1) 
                |Some x->
                   Some (x,head,index)
            |[]->None
        intern jobGroups scheduleOrder 0 *)
    ///<summary> Returns the next job to be scheduled from the Joblist of the given groupLevel removing it from the list</summary>
    /// <param name="jobGroups"> dictionary whos keys are the names of watchdirs and whos values are the list of jobs to run from that watchdir</param>
    /// <param name="scheduleOrder"> a list containg the keys from the dictionary that represents the order to choose by </pram>
    let removeNextItem (group:GroupLevel<'a>) scheduleOrder  =
        let rec intern _scheduleOrder index=
            match scheduleOrder with
            | head::tail-> 
                match tryGetValue head group with
                |Some {JobList=jobs}->
                    match removeJob jobs  with 
                    |None->
                        intern tail (index+1) 
                    |Some (job,newList)->
                        group.[head].JobList<-newList
                        Some (job,head,index)
                |None->
                    Logging.errorf "{JobRunner} jobList did not contain key '%A'from scheduleOrder" head
                    intern tail (index+1)
            |[]->None
        intern scheduleOrder 0
     ///<summary> Returns the next job to be scheduled from the Joblist of the given groupLevel removing it from the list</summary>
    /// <param name="jobGroups"> dictionary whos keys are the names of watchdirs and whos values are the list of jobs to run from that watchdir</param>
    /// <param name="scheduleOrder"> a list containg the keys from the dictionary that represents the order to choose by </pram>
    let removeNextItem2 (levels:RecDict<'a,'b,MutableData<JobItem<'c>list>>) scheduleOrder  =
        
        let rec intern ((mid,dat): (Dictionary<'a,MutableData<JobItem<'c>list>>* 'b option)) _scheduleOrder index =
            match scheduleOrder with
            | head::tail-> 
                match tryGetValue head mid with
                 |Some jobs->
                    match removeJob jobs.MutDat  with 
                    |None->
                        intern (mid,dat) tail (index+1) 
                    |Some (job,newList)->
                        dat.<-newList
                        Some (job,head,index)
                |None->
                    Logging.errorf "{JobRunner} jobList did not contain key '%A'from scheduleOrder" head
                    intern tail (index+1)
            |[]->None
        match levels with
        |Middle mid->intern mid scheduleOrder 0

    //TODO: may have to remove job from first group in this function
    let returnSchedule groupRoot (job:JobItem<'a>) =
        let rec intern (group:GroupLevel<'a>) scheduleTokens=
            group|>Seq.iter(fun x ->
            match x.Value with
            |{NextGroup=Some nextLevel}->
               group.[x.Key].ScheduleTokens.Add( List.head scheduleTokens)
               intern nextLevel (List.tail scheduleTokens)
            |{NextGroup=None}->
                ()
            )
        intern groupRoot (Seq.toList job.TakenScheduleTokens)
    ///Moves jobs from lower teirs of groups to higher teirs if possibble
    /// currently works top down which means it could take many iterations to mograte items up the group levels... i shold proabbly just run it regularly.
    let rec shuffleUp (group:GroupLevel<'a>)= 
        group|>Seq.iter(fun x ->
        match x.Value with
        |{NextGroup=Some nextLevel;JobList=jobList;ScheduleTokens=scheduleTokens}->
            
            match removeNextItem nextLevel (Seq.toList(scheduleTokens)) with
            |Some (job,name,index)->
                job.TakenScheduleTokens<-name::job.TakenScheduleTokens
                group.[x.Key].JobList<-job::jobList
                scheduleTokens.RemoveAt(index)
                if index=scheduleTokens.Count-1 then
                    shuffleUp nextLevel
                else
                    shuffleUp group
            |None->
                shuffleUp nextLevel
        |{NextGroup=None}->()
        )
    let rec shuffleUp2 (recDict:RecDict<string,Level<'a>,MutableData<JobList<'a>>>)= 
        match recDict with
        //First we figure out whetehr we are at the end of our Heirachy
        |Middle (dict,data)->
            //Recurse over all sub dictionaries first to make it run buttom up.
            dict|>Seq.iter(fun x-> shuffleUp2 x.Value)
            //We then iterate through the available tokens at this level and see if any jobs are available at the level below.
            //if a job is found it is moved the the current level and the token is moved from current levels "available" list to the jobs "taken" list.
            //The token removal is done by returning false and iterating using list.filter
            let newKeys=data.AvailableScheduleTokens |>List.filter (fun token ->
                let belowData=tryGetValue token dict
                match belowData with 
                |Some below ->
                    let updateList (list:ref<list<JobItem<'a>>>)=
                        match removeJob list.Value with
                        |Some (job,newList)->
                            list.Value<-newList
                            job.TakenScheduleTokens<-token::job.TakenScheduleTokens
                            data.JobList<-job::data.JobList
                            false
                        |None->true//This means there was no jobs waiting to move up
                    match below with
                    |Middle (_,mid)->updateList (ref mid.JobList)
                    |End en-> updateList (ref en.MutDat)
                |None-> true  
            )
            data.AvailableScheduleTokens<- newKeys
        |End data->()
        
    let addNewJob (groupRoot) job (groupkeys: string list)=
    //Here we keep drilling down thorugh groups using our list of keys untill we gt to the last key then insert out job at that point
        let rec intern (_group:GroupLevel<'a> option) keys=
            match _group with
            |Some group->
                match keys with
                |[only]->
                    if group.[only].NextGroup.IsSome then 
                        raise<| ArgumentException(sprintf"keys list did not drill all the way down to a watchdir, ended at key %s. Keys given: %A" only keys  )
                    group.[only].JobList<- job::group.[only].JobList
                |head::tail -> intern group.[head].NextGroup tail
            |None->raise <|ArgumentOutOfRangeException "Reached a group that didn't exist before getting to the end of the keys"
        intern groupRoot.NextGroup groupkeys
    let addNewJob2 (recDict:JobDB<'a>) job (groupkeys: string list)=
    //Here we keep drilling down thorugh groups using our list of keys untill we gt to the last key then insert out job at that point
        let data=drillToEndData recDict groupkeys
        match data with
        |Ok data->Ok (data.MutDat<-job::data.MutDat)
        |Error err->Error <|sprintf "failed with message %s "err
    let removeJob (groupRoot) job (groupkeys: string list)=
    //Here we keep drilling down thorugh groups using our list of keys untill we gt to the last key then insert out job at that point
        let rec intern (_group:GroupLevel<'a> option) keys=
            match _group with
            |Some group->
                match keys with
                |[only]->
                    if group.[only].NextGroup.IsSome then 
                        raise<| ArgumentException(sprintf"keys list did not drill all the way down to a watchdir, ended at key %s. Keys given: %A" only keys  )
                    group.[only].JobList<- job::group.[only].JobList
                |head::tail -> intern group.[head].NextGroup tail
            |None->raise <|ArgumentOutOfRangeException "Reached a group that didn't exist before getting to the end of the keys"
        intern groupRoot.NextGroup groupkeys

    (* let handleTopLevelJobs (group:GroupLevel<IO.Types.MoveJob>)=
        group.Values|>Seq.iter(fun x-> x.JobList|>List.iter(fun y->y.Job)) *)
        
        
        
        