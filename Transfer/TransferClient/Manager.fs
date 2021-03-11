namespace TransferClient

open Watcher
open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open TransferClient.Scheduling
open FSharp.Control
open System.Collections.Generic
open TransferHandling
open IO.Types
open System.Linq
open SharedFs.SharedTypes
open TransferClient.SignalR.ManagerCalls
open System.IO;
open Microsoft.AspNetCore
open ConfigReader

module Manager =
    ///Sets up things like the Jobdatabase and UIdata using the configuration read form the config file
    ///Returns the intialised UIData that contains the feilds taht will never change
    let initialiseDataStructures (configData:ConfigData) watchDirsData=
        //Extract all the needed groups
        let groups =
                watchDirsData
                |> List.map (fun x -> x.MovementData.GroupList)

        let mapping =
            configData.SourceIDMapping
            |> Seq.map (fun x -> KeyValuePair(x.Value, x.Key))
            |> Dictionary

        LocalDB.initDB groups configData.FreeTokens (processTask LocalDB.AcessFuncs) 
        // we set the ffmpegPath in the video mover.
        //TODO: this is really horrifyingly inelegant. However it does work
        Logging.infof "Setting ffmpeg Path to: %A" configData.FFmpegPath
        IO.VideoMover.ffmpegPath<- configData.FFmpegPath
        let heirachy=HierarychGenerator.makeHeirachy groups
        //This is the A UIData object with the unchanging parts filled out
        let baseUIData=(UIData mapping heirachy)
        baseUIData

    let setupSignalR configData baseUIData=
        async{
            let signalrCT = new Threading.CancellationTokenSource()
            //For reasons i entirely do not understand starting this just as async deosnt run connection in release mode
            Logging.infof "{Manager} starting signalr connection process"

            let conectionTask =
                SignalR.Commands.connect configData.manIP configData.ClientName baseUIData signalrCT.Token

            let! connection = conectionTask
            return connection
        }
    //TODO: i think i don't actually need the observable conversion here. I can probably just run AsyncStart in an asyncseq.parallel
    let scheduleAndCreateMoveJobs (newFilesForEachWatchdir:list<AsyncSeq<FoundFile array> * WatchDir>) receiverFuncs=
        let jobs =
                newFilesForEachWatchdir
                |> List.toArray
                |> Array.map (fun (schedules, watchDir) ->
                    Logging.infof "{Manager} Setting up observables for group: %A" watchDir.MovementData.GroupList
                    schedules
                    |>AsyncSeq.collect (fun (newFiles) ->
                        asyncSeq{
                            for file in newFiles do
                                let extension= (Path.GetExtension file.Path)
                                let transcode= 
                                    match watchDir.MovementData.TranscodeData with
                                    |Some x-> x.TranscodeExtensions|>List.contains extension
                                    |None-> false
                                
                                let task = Scheduler.scheduleTransfer file watchDir.MovementData (Some receiverFuncs) LocalDB.AcessFuncs 
                                Logging.infof "{Manager} created scheduling task for file %s" (Path.GetFileName file.Path)
                                yield task
                        }
                    )
                    |>AsyncSeq.toObservable
                        )
                    
                |>Observable.mergeArray
        jobs
    let startUp =
        async {
            //Read config file to get information about transfer source dest pairs
            let configData = ConfigReader.ReadFile "./WatchDirs.yaml"
            let mutable watchDirsData = configData.WatchDirs

            //Initialises the database and gives us a UIdata with unchanging fields filled.
            let baseUIData=initialiseDataStructures configData watchDirsData

            //create a asyncstream that yields newly found files
            let newFilesForEachWatchdir =
                watchDirsData|>List.map(fun watchDir->
                getNewFiles watchDir.MovementData.SourceFTPData watchDir.MovementData.DirData.SourceDir
                ,watchDir
                )

            
            let! connection = setupSignalR configData baseUIData           

            let getReceiverFuncs (signalRHub:SignalR.Client.HubConnection):ReceiverFuncs =
                let getReceiverIP receiverName=
                    SignalR.ManagerCalls.getReceiverIP signalRHub  receiverName
                let startReceiverInstance receiverName args=
                    SignalR.ManagerCalls.startReceiver signalRHub  receiverName args
                {GetReceiverIP=getReceiverIP;StartTranscodeReciever=startReceiverInstance}

            let receiverFuncs= getReceiverFuncs connection    
            //A list of scheduling tasks. These just need to be run at some point. 
            //All other parts of the job system happen due to events like new jobs being added and jobs completing.
            let scheduleJobs= scheduleAndCreateMoveJobs newFilesForEachWatchdir receiverFuncs

            //Start the Syncing Service
            //TODO: only start this if signalr connects sucesfully
            //let res= jobs|>Observable.mergeArray|>Observable.subscribe(fun x->x|>Async.StartImmediate)
            JobManager.Syncer.startSyncer LocalDB.jobDB.SyncEvents  500.0 (fun uiDat ->  Async.RunSynchronously (syncTransferData connection configData.ClientName uiDat)) baseUIData

            let runJobs = 
               // jobs|>Observable
                scheduleJobs|> Observable.map(fun x->x|>Async.Start)
            runJobs|>Observable.wait

            return! async {
                        while true do
                            do! Async.Sleep 100000
                    }

            
        }
