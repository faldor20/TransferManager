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
open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
module Manager =

    
    type jsonData = { WatchDirs: WatchDirSimple list }
    type yamlData = { WatchDirs: WatchDirSimple list }

    let mainLoop2 watchDir =
        let tasks = GetNewTransfers2 watchDir 
        tasks

    let sucessfullCompleteAction transferData groupName id source=
        printfn " successfully finished copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Complete; Percentage=100.0; EndTime=DateTime.Now} groupName id
    let FailedCompleteAction transferData groupName id source=
        printfn "failed copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Failed; EndTime=DateTime.Now} groupName id 
    let CancelledCompleteAction transferData groupName id source=
        printfn "canceled copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Cancelled; EndTime=DateTime.Now} groupName id

    let startUp =
        let configText = try File.ReadAllText("./WatchDirs.yaml")
                         with 
                            |IOException-> printfn "ERROR Could not find WatchDirs.yaml, that file must exist"
                                           "Failed To open 'WatchDirs.json' file must exist for program to run "
        let watchDirsUnfiltered = 
            match (Deserialize<yamlData> configText).[0] with
               |Success data -> printfn "Deserilaization Warnings: %A" data.Warn
                                data.Data
               |Error error ->failwith <|sprintf "Config file (%s) malformed, there is an error at %s becasue: %A" configText error.StopLocation.AsString error.Error
        // Here we check if the directry exists by getting dir and file info about the source and dest and
        //filtering by whether it triggers an exception or not
        
        let watchDirsExist= watchDirsUnfiltered.WatchDirs|>List.filter(fun dir->
            let destOkay= 
                let printError error= printfn "Watch Destination: %s for source:%s %s" dir.Destination dir.Source error
                try 
                    match dir.IsFTP with
                        | true-> 
                            //split into head(ip ) and tail(dir)
                            let ip::path=dir.Destination.Split '@'|>Array.toList
                            use client=new FluentFTP.FtpClient( ip,21,"quantel","***REMOVED***")
                            client.Connect()
                            let exists=client.DirectoryExists(path.[0])
                            if not exists then printError "could not be found on server" 
                            exists
                        |false-> (DirectoryInfo dir.Destination).Exists
                with
                    |(:? IOException)->  
                        printError "does not exist, will not watch this directory" 
                        false
                    | :? FluentFTP.FtpException-> 
                        printError "cannot be connected to" 
                        false
            let sourceOkay =
                try 
                    (DirectoryInfo dir.Source).Exists
                with
                    |_ ->printfn "Watch Source: %s for Destination:%s does not exist, will not watch this source" dir.Source dir.Destination 
                         false
            (sourceOkay && destOkay)
        )
        if watchDirsExist.Length=0 then  Console.Error.WriteLine("ERROR: no WatchDirs found in Json file. The program is usless without one")
        let mutable watchDirsData =
            watchDirsExist|> List.map (fun watchDir ->
                { 
                  GroupName= watchDir.GroupName
                  Dir = DirectoryInfo watchDir.Source
                  OutPutDir = watchDir.Destination
                  TransferedList = List.empty
                  IsFTP=watchDir.IsFTP
                  ScheduledTasks= List.Empty })
        
         // The key to this code is having an async seqence for each watchdir.
         // the asyncsequence triggers its map and iter functions for each new item as it is found
         //this means it kind of acts like an event trigger.
        
        watchDirsData|>List.iter(fun watchDir->printfn "Watching: %A" watchDir )
                   
        let schedules = mainLoop2  watchDirsData

        let resetWatch= 
            async{
                while true do
                    if DateTime.Now.TimeOfDay=TimeSpan.Zero then Data.reset()
                    do!Async.Sleep(1000*60)
        
            }
        Async.Start(resetWatch)
        
        let completeTransferTasks = asyncSeq{ 
            for directoryGroup in schedules do
                //This schedules the tasks it needs to be paralell because all transfers should be scheduled immidiatley
                //as soon as the file is detected
                let schedules,groupName=directoryGroup
                let scheduledTasks=schedules|>AsyncSeq.mapAsyncParallel(fun scheduleTask-> scheduleTask)

                //This iterates though the transfer tasks. It is "iterAsync" and not parallel
                //becuase we want the transfers to be started one after another
                yield scheduledTasks|> AsyncSeq.iterAsync (fun task ->
                     async{
                        let! transResult, id,ct = task

                        let transData=dataBase.[groupName].[id]
                        let source = dataBase.[groupName].[id].Source

                       //LOGGING: printfn "DB: %A" dataBase
                       
                        match transResult with 
                            |TransferResult.Success-> sucessfullCompleteAction transData groupName id source
                            |TransferResult.Cancelled-> CancelledCompleteAction transData groupName id source
                            |TransferResult.Failed-> FailedCompleteAction transData groupName id source
                       
                        let rec del path iterCount= async{
                            if iterCount>10 
                            then 
                                printfn"Error: Could not delete file at after trying for a minute : %s " path
                                return ()
                            else
                                try 
                                    File.Delete(path) 
                                with 
                                    |_-> do! Async.Sleep(1000)
                                         printfn "Error Couldn't delete file, probably in use somehow"
                                         do! del path (iterCount+1)
                            }
                        do! del source 0
                        
                    }
                )}
        
        completeTransferTasks|>AsyncSeq.iterAsyncParallel(fun x ->x)
    
        
