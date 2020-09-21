namespace TransferClient.DataBase

open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open Types
open TransferClient.JobManager
open Microsoft.AspNetCore.SignalR.Client

module LocalDB =
    let mutable ChangeDB =
        JobDataBase
    let mutable  jobDB = JobDataBase()
    //This is a saved copy of the database just after initialisation used for restting the database
    let mutable private freshDB= JobDataBase()
    let AcessFuncs = JobDBAccess jobDB

    let initDB (groups: int list list) freeTokens =
        TransferClient.Logging.infof "initialising DB"
        let scheduluIDLevel =
            groups
            |> List.collect List.indexed
            |> List.groupBy (fun (x, y) -> x)
            |> List.map (fun (_, y) ->  (y |> List.map (fun (_, y) -> y) |> List.distinct))
        let heirachyOrder =
            groups
            |> List.groupBy (fun x-> x.Length)
            |> List.sortBy(fun (x,y)-> x)
            |> List.map (fun (x,y)->y)
        jobDB.HierarchyOrder<- heirachyOrder 
        jobDB.FreeTokens<-freeTokens
        groups|>List.iter(fun x-> jobDB.JobHierarchy.[x]<-List.Empty)
        freshDB<-jobDB


    let reset () =
        TransferClient.Logging.infof "{DataBase} Resetting DataBase"
        jobDB<- freshDB
        

