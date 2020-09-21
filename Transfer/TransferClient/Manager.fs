namespace TransferClient

open Watcher
open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open FSharp.Control
open TransferHandling
open IO.Types
module Manager =

    let startUp =
        //Read config file to get information about transfer source dest pairs
        let configData= ConfigReader.ReadFile "./WatchDirs.yaml"
        let mutable watchDirsData= configData.WatchDirs
        let groups=watchDirsData|>List.map(fun x-> x.MovementData.GroupList)
        //Create all the needed groups
        LocalDB.initDB groups
        //create a asyncstream that yields new schedule jobs when 
        //a new file is detected in a watched source
        let schedulesInWatchDirs = GetNewTransfers2  watchDirsData LocalDB.AcessFuncs
        
        
        let signalrCT=new Threading.CancellationTokenSource()
        //For reasons i entirely do not understand starting this just as async deosnt run connection in release mode
        Logging.infof "{Manager} starting signalr connection process"
        let conection= SignalR.Commands.connect configData.manIP  configData.ClientName groups signalrCT.Token|>Async.RunSynchronously
        //Start the Syncing Service
        //TODO: only start this if signalr connects sucesfully
        (ManagerSync.DBsyncer 500 conection configData.ClientName )|>ignore
        

        //TODO: remove this and repalce it with a job that only runs when certain things happen. such as object removal object addition etc
        let shuffelJob=async{ 
                while true do
                    DataBase.LocalDB.AcessFuncs.Hierarchy.ShuffelUp()
                    do!Async.Sleep 1000
                }
        let checkForJobs=async{
            let rec jobCheck jobs=
                match DataBase.LocalDB.AcessFuncs.Hierarchy.GetTopJob() with
                |Some (jobID,location)->
                    let job=TransferHandling.processTask LocalDB.AcessFuncs  location jobID
                    jobCheck (job::jobs)
                |None->jobs
            while true do
                jobCheck []
                |>Async.Parallel
                |>Async.RunSynchronously
                |>ignore
                do! Async.Sleep(1000)
        }
        Async.Start shuffelJob
        Async.Start checkForJobs

        let jobs=schedulesInWatchDirs|>List.map(fun (schedules,grouList)->
            Logging.infof "Setting up observables for group: %A" grouList
            schedules|>AsyncSeq.iterAsyncParallel( fun x->x)
            )
        jobs|>Async.Parallel


        
