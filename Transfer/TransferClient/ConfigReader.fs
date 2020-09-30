namespace TransferClient
open System.IO
open System.Threading.Tasks
open FSharp.Data
open FSharp.Json
open System

open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
open IO.Types
open System.Collections.Generic
module ConfigReader=

    type ConfigMovementData =
        { GroupList: string list
          DirData: DirectoryData
          SourceFTPData: FTPData option
          DestFTPData: FTPData option
          TranscodeData: TranscodeData option }
      
    type YamlData = 
        {
            ManagerIP:string;
            ClientName:string; 
            MaxJobs:Dictionary<string,int>
            WatchDirs: ConfigMovementData list 
        }
    let directoryTest ftpData directory errorPrinter = 
        try 
            match ftpData with
            | Some data->   
                use client=new FluentFTP.FtpClient(data.Host,data.User,data.Password)
                Logging.infof "{Config} Testing connection to ftp: %A"data
                client.Connect()
                let exists=client.DirectoryExists(directory)
                if not exists then errorPrinter "could not be found on server" 
                exists
            |None-> 
                (DirectoryInfo directory).Exists
        with
            |(:? IOException)->  
                errorPrinter "does not exist, will not watch this directory" 
                false
            | :? FluentFTP.FtpException-> 
                errorPrinter "cannot be connected to" 
                false
            | x -> 
                errorPrinter (sprintf "not accessable for an handled reason \n reason: %A"  x.Message)
                false    

    //the simple watchDir is just a represntation of the exact object in the config file. It is used in deserialisation.
    let ReadFile configFilePath=
        Logging.infof "{Config} Reading config file at: %s" configFilePath

        let configText = try File.ReadAllText(configFilePath)
                         with 
                            | :? IOException->  Logging.errorf "{Config} Could not find WatchDirs.yaml, that file must exist"
                                                "Failed To open 'WatchDirs.yaml' file must exist for program to run "
        let yamlData = 
            match (Deserialize<YamlData> configText).[0] with
               |Success data -> 
                    Logging.infof "{Config}Deserilaization Warnings: %A" data.Warn
                    Logging.infof "{Config} Data=: %A" data.Data
                    data.Data
               |Error error ->
                    Logging.errorf "{Config}Config file (%s) malformed, there is an error at %s becasue: %A" configText error.StopLocation.AsString error.Error
                    raise (FormatException())

        // Here we check if the directry exists by getting dir and file info about the source and dest and
        //filtering by whether it triggers an exception or not
        
        let watchDirsExist= yamlData.WatchDirs|>List.filter(fun dir->

            let printDestError error= Logging.errorf "{Config} Watch Destination: %s for source:%s %s" dir.DirData.DestinationDir dir.DirData.SourceDir error
            let destOkay= 
                directoryTest dir.DestFTPData dir.DirData.DestinationDir printDestError
            let printSourceError error= Logging.errorf "{Config} Watch Source: %s for Destination:%s %s" dir.DirData.SourceDir dir.DirData.DestinationDir error
            let sourceOkay =
                directoryTest dir.SourceFTPData dir.DirData.SourceDir printSourceError
            (sourceOkay && destOkay)
        )
        if watchDirsExist.Length=0 then Logging.errorf "{Config} No WatchDirs with valid source and dest could be found in yaml file. The program is usless without one"
        let groups=
            watchDirsExist
                |>List.map(fun x-> x.GroupList)

        let mapping =
                    groups
                    |>List.concat
                    |> List.distinct
                    |> List.mapi (fun i x -> KeyValuePair(x, i))
                    |>Dictionary<string,int> 
        Logging.infof "{Config}Mapping: %A"mapping
        let freeTokens=yamlData.MaxJobs |>Seq.map(fun x ->  KeyValuePair(mapping.[x.Key],x.Value))|>Dictionary<int,int>
        let mappedGroups=
            groups
            |> List.map (List.map (fun x ->  mapping.[x]))
        let mutable watchDirsData =
            (mappedGroups, watchDirsExist)||> List.map2 (fun group watchDir ->
                
                let transData=
                    match watchDir.TranscodeData with 
                        |Some transData->
                            // We do this just incase someone does or does not put "." before extensions
                            let normalisedExtensions= transData.TranscodeExtensions|>List.map(fun item-> "."+(item.TrimStart '.'))
                            //This makes sre that an empty string is a none
                            let ffmpegArgs= transData.FfmpegArgs|> Option.bind (fun x->if x=""then None else Some x)
                            Some {transData with TranscodeExtensions= normalisedExtensions; FfmpegArgs=ffmpegArgs}
                        |None-> None
                let moveData:MovementData=
                   {    GroupList=group
                        DirData=watchDir.DirData
                        SourceFTPData=watchDir.SourceFTPData
                        DestFTPData=watchDir.DestFTPData
                        TranscodeData= transData
                   }
                
                {MovementData=moveData;TransferedList = List.empty;ScheduledTasks= List.Empty }
            )
        
        watchDirsData|>List.iter(fun watchDir->Logging.infof "Watching: %s" watchDir.MovementData.DirData.SourceDir )
        {|manIP= yamlData.ManagerIP; ClientName=yamlData.ClientName;FreeTokens=freeTokens;ScheduleIDMapping= mapping;WatchDirs= watchDirsData|}