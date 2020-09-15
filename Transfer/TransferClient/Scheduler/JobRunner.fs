namespace TransferClient.JobManager
open SharedFs.SharedTypes
open TransferClient.RecDict
open System
open FSharp.Control.Reactive
open TransferClient
open TransferClient.DataBase
open FSharp.Control
open TransferClient.IO.Types
open TransferClient.DataBase.Types
open System.Collections.Generic
module JobManager=
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
open JobManager
module JobRunner=

    let private tryGetValue key (gl)=
        let getVal (dic:Dictionary<'T,'U>)=
            if dic.ContainsKey(key) then
                Some dic.[key]
            else None
        getVal (gl)

    let private removeJob (jobsList:'a list)=
        if jobsList.Length>=1 then 
            Some (List.last jobsList,jobsList|> List.take (jobsList.Length-1))
        else None

    let returnSchedule' recDict scheduleTokens=
        let rec intern a tokens=
            match drillToData recDict a with
            |Ok data-> 
                match tokens with 
                |head::tail->
                    match data with
                    |MiddleType mid->
                        mid.AvailableScheduleTokens<- head::mid.AvailableScheduleTokens
                        let newList=a@[head]
                        intern a tail
                    |EndType _->Error <|sprintf"still had tokens left after reaching bottom of heirachy. Remaining: %A" tokens
                |[]->
                    match data with
                    |EndType _-> Ok ()
                    |MiddleType _->Error "Ran out of tokens before reaching Bottom of heirachy"
            |Error er->Error<| sprintf"failed drilling down %s"er
        intern [] scheduleTokens 
    let rec returnScheduleTokens'' (recDict:JobDB<'a>) (scheduleTokens) =
        //Check if we are at the end or not
        match recDict with
        |Middle (mid,dat)->
            //Get the head schedule token to be put back
            match scheduleTokens with 
            |head::tail->
                //Put token back
                dat.AvailableScheduleTokens<-(head):: dat.AvailableScheduleTokens
                //Go down a level and use that as the input for next time
                match mid.[head] with    
                |Middle nextMid->
                    returnScheduleTokens'' (Middle nextMid) (tail)
                |End _->
                    Ok()
            |[]->Error "Ran out of tokens before reaching Bottom of heirachy"
        |End _->
            match scheduleTokens with
            |_::_-> Error <|sprintf"still had tokens left after reaching bottom of heirachy. Remaining: %A" scheduleTokens
            |[]->Ok () 
    ///Moves jobs from lower teirs of groups to higher teirs if possibble
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
                    let updateList (jobList:list<JobItem<'a>>)=
                        match removeJob jobList with //remove the token from the list
                        |Some (job,newJobList)-> 
                            job.TakenScheduleTokens<-token::job.TakenScheduleTokens //give the job the token
                            data.JobList<-job::data.JobList //put the token in the tier aboves list
                            (newJobList,false) //return false which removes token from the available list
                        |None->(jobList,true)//This means there was no jobs waiting to move up
                    match below with
                    |Middle (_,mid)-> 
                        let (newList,outp)= updateList mid.JobList
                        mid.JobList<-newList
                        outp
                    |End en-> 
                        let (newList,outp)= updateList en.MutDat
                        en.MutDat<-newList
                        outp
                |None-> true  
            )
            data.AvailableScheduleTokens<- newKeys
        |End data->()
        
    
    let addNewJob2 (recDict:JobDB<'a>) job (groupkeys: string list)=
    //Here we keep drilling down thorugh groups using our list of keys untill we gt to the last key then insert out job at that point
        let data=drillToEndData recDict groupkeys
        match data with
        |Ok data->Ok (data.MutDat<-job::data.MutDat)
        |Error err->Error <|sprintf "failed with message %s "err
    ///Rmoves the Job from a list at the speified postion the position is a sequence of keys refering to its location n the heirache. an empty list means top level
    let removeJob2 (recDict:JobDB<'a>) (job:JobItem<'a>) (position: list<'b>)=
        let removeItem (list:ref<JobList<'a>>) =

            let newList=list.Value|>List.except [job] //remove job from list
            if newList.Length= (list.Value.Length-1) then
                list:=newList//apply change to list
                //We then return the tokens the job list
                match returnScheduleTokens'' recDict job.TakenScheduleTokens with
                |Ok _->Ok ()
                |Error er-> Error <| sprintf"failed returning tokens with: %s" er 
            else if newList.Length= list.Value.Length then
                Error "Job was not removed becuase it was not found in the list it was supposed to be removed from"
            else Error "Something went horribly horribly wrong"
        //Get the job wherever it may be
        let dat=drillToData recDict position
        match dat with
        |Ok data->
            match data with
            |EndType  x->removeItem (ref x.MutDat)
            |MiddleType x->removeItem (ref x.JobList)
        |Error err->Error <|sprintf "failed with message %s "err
    (* let handleTopLevelJobs (group:GroupLevel<IO.Types.MoveJob>)=
        group.Values|>Seq.iter(fun x-> x.JobList|>List.iter(fun y->y.Job)) *)
        
        
        
        