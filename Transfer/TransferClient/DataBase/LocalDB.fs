namespace TransferClient.DataBase

open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open Types
open TransferClient.JobManager
open Microsoft.AspNetCore.SignalR.Client

module LocalDB =
    let mutable ChangeDB :JobDataBase= JobDataBase (fun _ _->())
    let mutable  jobDB:JobDataBase =  JobDataBase(fun _ _->())
    //This is a saved copy of the database just after initialisation used for restting the database
    let mutable private freshDB:JobDataBase= JobDataBase (fun _ _->())
    let AcessFuncs = access jobDB

    let initDB (groups: int list list) freeTokens runJob =
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
        
        jobDB.Sources<-sources
        jobDB.FreeTokens<-freeTokens
        jobDB.RunJob<-runJob
        (* groups|>List.iter(fun x-> jobDB.JobHierarchy.[x]<-List.Empty) *)
        freshDB<-jobDB



    let reset () =
        TransferClient.Logging.infof "{DataBase} Resetting DataBase"
        jobDB<- freshDB
        

