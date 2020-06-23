namespace Transfer

open System.IO
open System.Threading.Tasks
open Mover
open Watcher
open FSharp.Data
open FSharp.Json
open System
open IOExtensions
module Manager =
    type WatchDir = { Source: string; Destination: string }
    type jsonData = { WatchDirs: WatchDir list }

    let mainLoop watchDir =
        let tasks, dirs = iterFolders watchDir |> List.unzip
        (tasks, dirs)


    let sucessfullCompleteAction id source=
        printfn "finished copying %A" source
        Data.setTransferData { (Data.getTransferData().[id]) with Status=SharedData.TransferStatus.Complete; Percentage=100.0 } id
    let FailedCompleteAction id source=
        printfn "finished copying %A" source
        Data.setTransferData { (Data.getTransferData().[id]) with Status=SharedData.TransferStatus.Failed; } id
    let CancelledCompleteAction id source=
        printfn "finished copying %A" source
        Data.setTransferData { (Data.getTransferData().[id]) with Status=SharedData.TransferStatus.Cancelled; } id

    let startUp =
        let json = try File.ReadAllText("./WatchDirs.json")
                   with 
                       |IOException-> printfn "Could not find WatchDirs.json, that file must exist"
                                      "Failed To open 'WatchDirs.json' file must exist for program to run "
        let watchDirsUnfiltered = 
            try Json.deserialize<jsonData> json
            with |JsonDeserializationError -> failwith "Json file malformed, there is an error somewhere"
        // Here we check if the directry exists by getting dir and file info about the source and dest and
        //filtering by whether it triggers an exception or not
        
        let watchDirsExist= watchDirsUnfiltered.WatchDirs|> List.filter(fun dir->
            let destOkay= 
                try 
                    (DirectoryInfo dir.Destination).Exists
                with
                    |_->    printfn"Watch Destination: %s for source:%s does not exist, will not watch this directory" dir.Destination dir.Source
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
                { Dir = DirectoryInfo watchDir.Source
                  OutPutDir = DirectoryInfo watchDir.Destination
                  TransferedList = List.empty })
        watchDirsData|>List.iter(fun watchDir->printfn "Watching: %A" watchDir )
        let mutable currentTasks = list.Empty
        

        async{
            //have a list of running tasks and scheduled tasks 
            //loop (limit-runningtasks) into the scheduled tasks starting them and moving them to the running list 
            //the list will be a dictionary once the task is complete it s guid can be used to remove it
            while true do
               
                let tasks, newWatchDir = mainLoop watchDirsData
                if  tasks.Length>0 then if tasks.[0].Length>0 then currentTasks <- currentTasks @ tasks
                watchDirsData <- newWatchDir
                
                
                let a = tasks|> List.map (fun item ->
                            item|> Array.map (fun task ->
                                async{
                                    let! transResult, id,ct = task
                                    do! Async.Sleep(50)
                                    let source = Data.data.[id].Source
                                    
                                    match transResult with 
                                        |TransferResult.Success-> sucessfullCompleteAction id source
                                        |TransferResult.Cancelled-> CancelledCompleteAction id source
                                        |TransferResult.Failed-> FailedCompleteAction id source
                                   
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
                            ))
                a|>List.iter (fun b->b|>Array.iter(fun ass-> ass |>Async.Start))
                
                //printfn " current state=%A" Data.data
                do!Async.Sleep(1000) 

            }    
        
