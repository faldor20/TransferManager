module TransferClient.ConfigReader.Reader
open System.IO
open System

//open Legivel.Serialization
open Thoth.Json.Net
open FSharp.Control
open Mover.Types
open System.Collections.Generic
open FSharp
open LoggingFsharp
open TransferClient.ConfigReader.Types


//the simple watchDir is just a represntation of the exact object in the config file. It is used in deserialisation.
let ReadFile (configFilePath:string)=
    Lginfo "'Config' Reading config file at: {config}" configFilePath

    let configText = try File.ReadAllText(configFilePath)
                     with 
                        | :? IOException->  Lgerrorf "'Config' Could not find WatchDirs.yaml, that file must exist"
                                            "Failed To open 'WatchDirs.yaml' file must exist for program to run "
    
    
    let yamlData2 = 
        match Decode.Auto.fromString<YamlData>( configText) with
           |Ok data -> 
                data
           |Error err  ->
                printfn "'Config'Config file (%s) malformed, there is an error: %s" configText err
                Lgerror2 "'Config'Config file ({@conf}) malformed, there is an error: {err}" configText err
                failwith "failed"
    let yamlData= yamlData2

    let programGroupName=yamlData.ClientName+"Limit"
    let watchDirsExist= Checkers.getValidWatchDirs yamlData.WatchDirs
    //We preppend ProgramLimit to all groupList list becuase that is the top level group set by "TotalMaxJobs"
    let watchDirs= watchDirsExist|>List.map (fun wd ->{wd with  GroupList= (programGroupName)::wd.GroupList})
    let groups=
        watchDirs
            |>List.map(fun x-> x.GroupList)

    let mapping =
                groups
                |>List.concat
                |> List.distinct
                |> List.mapi (fun i x -> KeyValuePair(x, i))
                |>Dictionary<string,int> 
    Lginfo "'Config'Mapping: {@mapping}"mapping
    //Add the 'ProgramLimit' Group that reprents the max concurrent jobs the client is ever able to run
    let maxJobs= yamlData.GroupMaxJobs.Add(programGroupName,yamlData.TotalMaxJobs)
    let freeTokens= Checkers.getTokensForGroups maxJobs mapping
        
    let mappedGroups=
        groups
        |> List.map (List.map (fun x ->  mapping.[x]))

    let mutable watchDirsData =
        (mappedGroups, watchDirs)||> List.map2 (fun group watchDir ->
            
            let transData=
                match watchDir.TranscodeData with 
                    |Some transData->
                        // We do this just incase someone does or does not put "." before extensions
                        let normalisedExtensions= transData.TranscodeExtensions|>List.map(fun item-> "."+(item.TrimStart '.'))
                        //This makes sure that an empty string is a none
                        let noneIfWhitespace str=
                            str|> Option.filter (String.IsNullOrWhiteSpace>>not)
                        let ffmpegArgs= transData.FfmpegArgs|> Option.filter (String.IsNullOrWhiteSpace>>not)
                        Some {transData with TranscodeExtensions= normalisedExtensions; FfmpegArgs=ffmpegArgs}
                    |None-> None
            let moveData:MovementData=
               {    GroupList=group
                    DirData=watchDir.DirData
                    SourceFTPData=watchDir.SourceFTPData
                    DestFTPData=watchDir.DestFTPData
                    TranscodeData= transData
                    SleepTime=watchDir.SleepTime
               }
            
            {MovementData=moveData;TransferedList = List.empty;ScheduledTasks= List.Empty }
        )
    
    watchDirsData|>List.iter(fun watchDir->Lginfo "'Config' Sources that will be watched: {@watchdirs}" watchDir.MovementData.DirData.SourceDir )
    {DisplayPriority=yamlData.DisplayPriority;FFmpegPath=yamlData.FFmpegPath; manIP= yamlData.ManagerIP; ClientName=yamlData.ClientName;FreeTokens=freeTokens;SourceIDMapping= mapping;WatchDirs= watchDirsData}