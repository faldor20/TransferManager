namespace Transfer
open System.IO
open System.Threading.Tasks
open FSharp.Data
open FSharp.Json
open System
open IOExtensions
open Data;
open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
module ConfigReader=
    type jsonData = { WatchDirs: WatchDirSimple list }
    type YamlData = { WatchDirs: WatchDirSimple list }
    let ReadFile configFilePath=
            let configText = try File.ReadAllText(configFilePath)
                             with 
                                |IOException-> printfn "ERROR Could not find WatchDirs.yaml, that file must exist"
                                               "Failed To open 'WatchDirs.yaml' file must exist for program to run "
            let watchDirsUnfiltered = 
                match (Deserialize<YamlData> configText).[0] with
                   |Success data -> printfn "Deserilaization Warnings: %A" data.Warn
                                    printfn "Config Data=: %A" data.Data
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
                        | _-> 
                            printError "watchdir dest not accessable for an unknown reason" 
                            false    
                let sourceOkay =
                    try 
                        (DirectoryInfo dir.Source).Exists
                    with
                        |_ ->printfn "Watch Source: %s for Destination:%s does not exist, will not watch this source" dir.Source dir.Destination 
                             false
                (sourceOkay && destOkay)
            )
            if watchDirsExist.Length=0 then  printfn "ERROR: no WatchDirs existing could be found in yaml file. The program is usless without one"
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
            watchDirsData