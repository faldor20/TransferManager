namespace TransferClient.DataBase

open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open Types
open TransferClient.JobManager
open TransferClient.JobManager.Main
open Microsoft.AspNetCore.SignalR.Client
open SharedFs.SharedTypes

module LocalDB =
   // let mutable ChangeDB :JobDataBase= JobDataBase (fun _ _->async{()}) (Dictionary())
    let  jobDB:JobDataBase =  JobDataBase(fun _ _->async{()}) (Dictionary())(Array.empty)
    //This is a saved copy of the database just after initialisation used for restting the database
    let mutable private freshDB:JobDataBase= JobDataBase (fun _ _->async{()}) (Dictionary())(Array.empty)
    
    let  AcessFuncs = access jobDB

    let initDB (groups: int list list) (freeTokens:Dictionary<int,int>) runJob iDMapping heirachy=
        //the groups that is passed in should be each of the watchdirs "GroupList"
        TransferClient.Logging.infof "initialising DB"
        let scheduleIDLevel =
            groups
            |> List.collect List.indexed
            |> List.groupBy (fun (x, y) -> x)
            |> List.map (fun (_, y) ->  (y |> List.map (fun (_, y) -> y) |> List.distinct))
        let sources =
            groups
            |>List.map(fun tokens->KeyValuePair( List.last tokens,{Source.Jobs=new List<JobItem>();Source.RequiredTokens=tokens}))
            |>Dictionary<int,Source>
        let tokens=
            freeTokens|>Seq.map(fun token ->
                //A list of all the sources that would want this token
                let sources=
                    groups
                    |>List.filter (List.contains token.Key)
                    |>List.map List.last

                KeyValuePair( token.Key,{Token=token.Key;Remaining= token.Value;SourceOrder=sources }))
            |>Dictionary
        let db={
            jobDB with
                Sources=sources
                FreeTokens=tokens
                RunJob=runJob
                
        }
        jobDB.Sources<-sources
        jobDB.FreeTokens<-tokens
        jobDB.RunJob<-runJob
        
        (* groups|>List.iter(fun x-> jobDB.JobHierarchy.[x]<-List.Empty) *)
        freshDB<-db



    let reset () =
        TransferClient.Logging.infof "{DataBase} Resetting DataBase"
        //We have to do this ugly monstrosity because if i make the jobDB mutable when it is reassigned Acessfuncs will point to a previous version of it
        jobDB.RunningJobs.Clear()
        jobDB.JobOrder.Clear()
        jobDB.JobList.Clear()
        jobDB.FinishedJobs.Clear()
        jobDB.TransferDataList.Clear()
        jobDB.FreeTokens<-freshDB.FreeTokens
        jobDB.Sources<-freshDB.Sources
        jobDB.RunJob<-freshDB.RunJob
       

       
        

