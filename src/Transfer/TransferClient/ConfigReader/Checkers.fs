module TransferClient.ConfigReader.Checkers
open System
open TransferClient.ConfigReader.Types
open System.Collections.Generic

open LoggingFsharp
open FluentFTP
open Mover.Types
open System.IO
module Seq=
    ///Converts a seq to an option where None is lengt=0 and Some is length>0
    let inline toOption (s:seq<'a>)=
        if Seq.isEmpty s then None else (Some s)  
let checkMaxJobs (maxJobs:IReadOnlyDictionary<string,'b>) (groupMapping:Dictionary<string,'a>) =
    
    //Here we check to make sure the MaxJobs contains one entry for each group given to watchDirs
    let missingGroups=groupMapping.Keys|>Seq.except (maxJobs|>Seq.map(fun x->x.Key))|>Seq.toList
    if missingGroups.Length>0 then
        Lgerror2 "'Config' The groups: '{@groups}' do not have entries in 'Maxjobs'. Add those groups to 'MaxJobs' eg: [\"{@first}\":\"5\"] " missingGroups (Seq.head missingGroups)
let getTokensForGroups (maxJobs:IReadOnlyDictionary<string,'b>) groupMapping=
            checkMaxJobs maxJobs groupMapping

            maxJobs|>Seq.choose(fun x ->  
                if(groupMapping.ContainsKey x.Key) then 
                    Some<|KeyValuePair(groupMapping.[x.Key],x.Value)
                else 
                    Lgerror "'Config' One of the group names: '{@groupName}' in 'MaxJobs' is not used by any WatchDir entries. This may mean you have misspelt something or have just removed a WatchDir entry without remvoing the groupName from MaxJobs  " x.Key
                    None ) 
            |>Dictionary<int,int>
///Performs some basic tests to check if a directory exists. Works using ftp or otherwise.
let directoryTest (ftpData) directory errorPrinter = 
    
    try 
        match ftpData with
        | Some data->   
            use client=new FluentFTP.FtpClient(data.Host,data.User,data.Password)
            Lginfo "'Config' Testing connection to ftp: {config}" data
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
/// Here we check if the directry exists by getting dir and file info about the source and dest and
///filtering by whether it triggers an exception or not
let getValidWatchDirs (watchDirs:ConfigMovementData list)=
    let validWatchDirs=watchDirs|>List.filter(fun dir->

        let printDestError error= Lgerror3 "'Config' Watch Destination: {@destDir} for source:{@source} {@error}" dir.DirData.DestinationDir dir.DirData.SourceDir error
        let destOkay= 
            match  dir.TranscodeData|>Option.bind(fun x->x.ReceiverData) with
            |None->directoryTest dir.DestFTPData dir.DirData.DestinationDir printDestError
            |Some(x)-> true //TODO: I may want to add some kind of complex check that sends out a question to the client asking if it is available.

        let printSourceError error= Lgerror3 "'Config' Watch Source:  {@source} for source:{@dest} {@error}" dir.DirData.SourceDir dir.DirData.DestinationDir error
        let sourceOkay =
            directoryTest dir.SourceFTPData dir.DirData.SourceDir printSourceError
        (sourceOkay && destOkay)
    )
    if validWatchDirs.Length=0 then Lgerrorf "'Config' No WatchDirs with valid source and dest could be found in yaml file. The program is usless without one"        
    validWatchDirs