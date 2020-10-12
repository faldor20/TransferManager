namespace TransferClient

open Watcher
open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open FSharp.Control
open System.Collections.Generic
open TransferHandling
open IO.Types
open System.Linq
open SharedFs.SharedTypes
open TransferClient.SignalR.ManagerCalls

module Manager =
    ///this ungodly monstrosity transforms the input into a list that has the distinct items from every layer of the groups
    ///eg: [[a,b,k],[a,c,d],[a,b,g]] becomes[[a],[b,c],[d,g,k]]
    let transformGroups groups =
        groups
        |> List.collect List.indexed
        |> List.groupBy (fun (x, y) -> x)
        |> List.map (fun (_, y) -> (y |> List.map (fun (_, y) -> y) |> List.distinct))
    //for each group there is now a key in a dictionary whos value is the groups that are below that group in the heirachy
     ///eg: [[a,b,k],[a,c,d],[a,b,g]] becomes [ {a:[b,c]} ,{b:[k,g] c:[d]}]
    let makeHeirachy (groups: int list list) =
        let longestGroup =
            groups|> List.fold (fun y x -> if y < x.Length then x.Length else y) 0

        let output: Dictionary<int, List<int>> list =
            List.init longestGroup (fun x -> Dictionary())

        //initialise all the lists
        let distinctGroups = transformGroups groups
        (output, distinctGroups)
        ||> List.iter2 (fun out lis ->
                lis
                |> List.iter (fun x -> out.[x] <- new List<int>()))
        //give each group its children in the heirachy
        groups
        |> List.iter (fun x ->
            x
            |> List.pairwise
            |> List.iteri (fun i (x1, x2) -> 
            output.[i].[x1].Add(x2)))
        //we may have duplicate entries in the heirachy so we should distinctify it
        let out=
            output
            |>List.map (fun  dic-> 
                dic
                |>(Seq.map(fun entry ->
                    KeyValuePair(entry.Key, dic.[entry.Key].Distinct().ToList())))
                    |>Dictionary 
                    )

        out|>List.toArray

    let startUp =
        async {
            //Read config file to get information about transfer source dest pairs
            let configData = ConfigReader.ReadFile "./WatchDirs.yaml"
            let mutable watchDirsData = configData.WatchDirs
            //Create all the needed groups
            let groups =
                watchDirsData
                |> List.map (fun x -> x.MovementData.GroupList)

            let mapping =
                configData.ScheduleIDMapping
                |> Seq.map (fun x -> KeyValuePair(x.Value, x.Key))
                |> Dictionary
            let heirachy=makeHeirachy groups
            LocalDB.initDB groups configData.FreeTokens (processTask LocalDB.AcessFuncs) mapping heirachy

            //This is the A UIData object with the unchanging parts filled out
            let baseUIData=(UIData mapping heirachy)
            //create a asyncstream that yields new schedule jobs when
            //a new file is detected in a watched source
            let schedulesInWatchDirs =
                GetNewTransfers2 watchDirsData LocalDB.AcessFuncs


            let signalrCT = new Threading.CancellationTokenSource()
            //For reasons i entirely do not understand starting this just as async deosnt run connection in release mode
            Logging.infof "{Manager} starting signalr connection process"

            let conectionTask =
                SignalR.Commands.connect configData.manIP configData.ClientName baseUIData signalrCT.Token

            let jobs =
                schedulesInWatchDirs
                |> List.toArray
                |> Array.map (fun (schedules, grouList) ->
                    Logging.infof "{Manager}Setting up observables for group: %A" grouList
                    schedules|> AsyncSeq.toObservable
                    )
                |>Observable.mergeArray




            //Start the Syncing Service
            //TODO: only start this if signalr connects sucesfully
            //let res= jobs|>Observable.mergeArray|>Observable.subscribe(fun x->x|>Async.StartImmediate)
            let! conection = conectionTask
            JobManager.Syncer.startSyncer LocalDB.jobDB.SyncEvents  500.0 (fun uiDat ->  Async.RunSynchronously (syncTransferData conection configData.ClientName uiDat)) baseUIData

            let runJobs = 
               // jobs|>Observable
                jobs|> Observable.map(fun x->x|>Async.Start)
            runJobs|>Observable.wait

            return! async {
                        while true do
                            do! Async.Sleep 100000
                    }

            
        }
