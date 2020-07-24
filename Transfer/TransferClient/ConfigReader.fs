namespace TransferClient
open System.IO
open System.Threading.Tasks
open FSharp.Data
open FSharp.Json
open System
open IOExtensions
open Data.Types;
open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
module ConfigReader=

    //the simple watchDir is just a represntation of the exact object in the config file. It is used in deserialisation.
    type jsonData = { WatchDirs: MovementData list }
    type YamlData = { WatchDirs: MovementData list }
    let ReadFile configFilePath=
        printfn "Reading config file at: %s" configFilePath

        let configText = try File.ReadAllText(configFilePath)
                         with 
                            | :? IOException->  printfn "ERROR Could not find WatchDirs.yaml, that file must exist"
                                                "Failed To open 'WatchDirs.yaml' file must exist for program to run "
        let watchDirsUnfiltered = 
            match (Deserialize<YamlData> configText).[0] with
               |Success data -> 
                    printfn "Deserilaization Warnings: %A" data.Warn
                    printfn "Config Data=: %A" data.Data
                    data.Data
               |Error error ->
                    printfn "Config file (%s) malformed, there is an error at %s becasue: %A" configText error.StopLocation.AsString error.Error
                    raise (FormatException())

        // Here we check if the directry exists by getting dir and file info about the source and dest and
        //filtering by whether it triggers an exception or not
        
        let watchDirsExist= watchDirsUnfiltered.WatchDirs|>List.filter(fun dir->
            let destOkay= 
                let printError error= printfn "Watch Destination: %s for source:%s %s" dir.DirData.DestinationDir dir.DirData.DestinationDir error
                try 
                    match dir.FTPData with
                        | Some ftpData->   
                            use client=new FluentFTP.FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
                            client.Connect()
                            let exists=client.DirectoryExists(dir.DirData.DestinationDir)
                            if not exists then printError "could not be found on server" 
                            exists
                        |None-> 
                            (DirectoryInfo dir.DirData.DestinationDir).Exists
                with
                    |(:? IOException)->  
                        printError "does not exist, will not watch this directory" 
                        false
                    | :? FluentFTP.FtpException-> 
                        printError "cannot be connected to" 
                        false
                    | _ as x -> 
                        printError "watchdir dest not accessable for an unknown reason" 
                        printfn "reason: %A" x.Message
                        false    
            let sourceOkay =
                try 
                    (DirectoryInfo dir.DirData.SourceDir).Exists
                with
                    |_ ->printfn "Watch Source: %s for Destination:%s does not exist, will not watch this source" dir.DirData.SourceDir dir.DirData.DestinationDir 
                         false
            (sourceOkay && destOkay)
        )
        if watchDirsExist.Length=0 then printfn "ERROR: no WatchDirs existing could be found in yaml file. The program is usless without one"
       
        let mutable watchDirsData =
            watchDirsExist|> List.map (fun watchDir ->
                let transData=
                    match watchDir.TranscodeData with 
                        |Some transData->
                            // We do this just incase someone does or does not put "." before extensions
                            let normalisedExtensions= transData.TranscodeExtensions|>List.map(fun item-> "."+(item.TrimStart '.'))
                            //This makes sre that an empty string is a none
                            let ffmpegArgs= transData.FfmpegArgs|> Option.bind (fun x->if x=""then None else Some x)
                            Some {transData with TranscodeExtensions= normalisedExtensions; FfmpegArgs=ffmpegArgs}
                        |None-> None
                let moveData=
                   { watchDir with
                        TranscodeData= transData
                   }
                {MovementData=moveData;TransferedList = List.empty;ScheduledTasks= List.Empty }
            )
        
        watchDirsData|>List.iter(fun watchDir->printfn "Watching: %A" watchDir )
        watchDirsData