namespace TransferClient

open Watcher
open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open FSharp.Control
open TransferHandling
open IO.Types
open RecDict
module Manager =

    let startUp =
        //Read config file to get information about transfer source dest pairs
        let managerIP,userName,rest= ConfigReader.ReadFile "./WatchDirs.yaml"
        let mutable watchDirsData= rest
        let groupsRes=
            watchDirsData
            |>List.map(fun x->   x.MovementData.Grouping)
            |>makeKeys
        let groups=
            match groupsRes with 
            |Ok a-> a
            |Error a-> failwith a
        //Create all the needed groups
        LocalDB.initRecDB groups
        //create a asyncstream that yields new schedule jobs when 
        //a new file is detected in a watched source
        let schedulesInWatchDirs = GetNewTransfers2  watchDirsData LocalDB.AccessFuncs
        
        
        let signalrCT=new Threading.CancellationTokenSource()
        //For reasons i entirely do not understand starting this just as async deosnt run connection in release mode
        Logging.infof "{Manager} starting signalr connection process"
        let conection= SignalR.Commands.connect managerIP  userName groups signalrCT.Token|>Async.RunSynchronously
        //Start the Syncing Service
        //TODO: only start this if signalr connects sucesfully
        (ManagerSync.DBsyncer 500 conection userName )|>ignore
        

    
        //Convert the asyncseq to an observable. This is like start all the schedule tasks in
        //paralell but then only interacting with the sequentially as they complete.
        //This is done becuase the sceduling must be started as soon as a new file is found
        //but because we only want one transfer to happen at a time the transfer tasks that
        // are finished shceduling need to be processed sequentially
        let observables= schedulesInWatchDirs|>List.map(fun (schedules,groupName)->
            Logging.infof "Setting up observables for group: %s" groupName
            schedules
                |>AsyncSeq.toObservable
                |>Observable.bind (fun x-> Observable.ofAsync x)
                |>Observable.iter(fun transferTask ->
                    Async.RunSynchronously( processTask transferTask ))
          
            )
        //Merge the observable seqence of each group together
        let outPut=observables|>Observable.mergeSeq
        //"Start" the observables(This reall ust stops he thread from ever 
        //completing allowing the observable to contine running)
        outPut|>Observable.wait
        
