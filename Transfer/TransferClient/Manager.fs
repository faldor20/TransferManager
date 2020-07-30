namespace TransferClient

open System.IO
open System.Threading.Tasks
open TransferClient.IO
open Watcher
open FSharp.Data
open FSharp.Json
open System
open FSharp.Control.Reactive
open SharedFs.SharedTypes
open Legivel.Serialization
open TransferClient.DataBase
open FSharp.Control
open TransferHandling
open IO.Types
module Manager =


    let startUp =
        //Read config file to get information about transfer source dest pairs
        let userName,rest= ConfigReader.ReadFile "./WatchDirs.yaml"
        let mutable watchDirsData= rest
        //create a asyncstream that yields new schedule jobs when 
        //a new file is detected in a watched source
        let schedulesInWatchDirs = GetNewTransfers2  watchDirsData LocalDB.AccessFuncs
        
        let groups=watchDirsData|>List.map(fun x-> x.MovementData.DirData.GroupName)
        let signalrCT=new Threading.CancellationTokenSource()

        Async.Start (SignalR.Commands.MakeConnection userName groups signalrCT.Token)

        //Start the Syncing Service
        //TODO: only start this if signalr connects sucesfully
        let res= (DataBase.ManagerSync.DBsyncer 500) userName
    
        //Convert the asyncseq to an observable. This is like start all the schedule tasks in
        //paralell but then only interacting with the sequentially as they complete.
        //This is done becuase the sceduling must be started as soon as a new file is found
        //but because we only want one transfer to happen at a time the transfer tasks that
        // are finished shceduling need to be processed sequentially
        let observables= schedulesInWatchDirs|>List.map(fun (schedules,groupName)->
            printfn "Setting up observables for group: %s" groupName
            schedules
                |>AsyncSeq.toObservable
                |>Observable.bind (fun x-> Observable.ofAsync x)
                |>Observable.iter(fun transferTask ->
                    Async.Start( processTask transferTask ))
          
            )
        //Merge the observable seqence of each group together
        let outPut=observables|>Observable.mergeSeq
        //"Start" the observables(This reall ust stops he thread from ever 
        //completing allowing the observable to contine running)
        outPut|>Observable.wait
        
