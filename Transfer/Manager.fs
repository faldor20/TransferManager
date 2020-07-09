namespace Transfer

open System.IO
open System.Threading.Tasks
open Mover
open Watcher
open FSharp.Data
open FSharp.Json
open System
open IOExtensions
open Data;
open FSharp.Control.Reactive
open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
open TransferHandling
module Manager =


    let startUp =
        //Read config file to get information about transfer source dest pairs
        let mutable watchDirsData= ConfigReader.ReadFile "./WatchDirs.yaml"
        //create a asyncstream that yields new schedule jobs when 
        //a new file is detected in a watched source
        let schedulesInWatchDirs = GetNewTransfers2  watchDirsData
        
        //Start the task that clears the history of transfers at 00:00
        Async.Start(resetWatch)
    
        //Convert the asyncseq to an observable. This is like start all the schedule tasks in
        //paralell but then only interacting with the sequentially as they complete.
        //This is done becuase the sceduling must be started as soon as a new file is found
        //but because we only want one transfer to happen at a time the transfer tasks that
        // are finished shceduling need to be processed sequentially
        let observables= schedulesInWatchDirs|>List.map(fun (schedules,groupName)->
            printfn "Setting up observables for group: %s" groupName
            schedules
                |>AsyncSeq.toObservable
                |>Observable.bind Observable.ofAsync
                |>Observable.iter(fun transferTask ->
                    Async.Start( processTask groupName transferTask))
          
            )
        //Merge the observable seqence of each group together
        let outPut=observables|>Observable.mergeSeq
        //"Start" the observables(This reall ust stops he thread from ever 
        //completing allowing the observable to contine running)
        outPut|>Observable.wait
        
