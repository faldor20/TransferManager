namespace TransferClient

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open IO.Types
open FluentFTP
open FSharp.Control
open FSharp.Control.Reactive.Builders
module Watcher =

//======================
// Here we have the new version of the scheduling code that uses async sequences
//====================
    let checkForNewFilesFTP (connection:FtpClient) (ignoreList: string []) (folder)=
        connection.Connect()
        let fileNames=
            connection.GetListing()
            |>Array.filter(fun item->item.Type=FtpFileSystemObjectType.File)
            |>Array.map(fun item->folder+item.Name )
            |>Array.except ignoreList
        connection.Disconnect()
        fileNames
    
    ///Returns the filePaths of files not part of the ignorelist
    let checkForNewFiles2 (ignoreList: string []) (folder) =
       
        DirectoryInfo( folder).GetFiles()|>Array.map(fun i-> i.FullName)|> Array.except ignoreList
    

    let ActionNewFiles2 dbAcess (watchDir:WatchDir)   =
        (asyncSeq{ 
            let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
            let ftpConnection= watchDir.MovementData.SourceFTPData|>Option.bind (fun x->Some (new FluentFTP.FtpClient(x.Host,x.Password,x.User)))

            while true do
                let newFilesFunc=
                    match ftpConnection with 
                    |Some con->checkForNewFilesFTP con 
                    |None-> checkForNewFiles2 
                let newFiles=newFilesFunc ignoreList watchDir.MovementData.DirData.SourceDir   
                for file in newFiles do
                    let extension= (Path.GetExtension file)
                    let transcode= 
                        match watchDir.MovementData.TranscodeData with
                        |Some x-> x.TranscodeExtensions|>List.contains extension
                        |None-> false
                    
                    let task = Scheduler.scheduleTransfer file watchDir.MovementData dbAcess transcode
                    Logging.infof "{Found} and created scheduled task for file %s" (Path.GetFileName file)
                    yield task
                ignoreList<- ignoreList|> Array.append newFiles
                do! Async.Sleep(500);
        },watchDir.MovementData.DirData.GroupName)

   
    let GetNewTransfers2 watchDirs dbAccess=
        let tasks=watchDirs|>List.map (ActionNewFiles2 dbAccess)
        tasks