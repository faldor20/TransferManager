namespace TransferClient

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open IO.Types
open FluentFTP
open FSharp.Control
open FSharp.Control.Reactive.Builders
open System
module Watcher =

//======================
// Here we have the new version of the scheduling code that uses async sequences
//====================
    let checkForNewFilesFTP (connection:FtpClient) (ignoreList: string []) (folder)=
        let fileList=connection.GetListing(folder)
        let fileNames=
            fileList
            |>Array.filter(fun item->item.Type=FtpFileSystemObjectType.File)
            |>Array.map(fun x-> { Path= x.FullName;FTPFileInfo=Some x})
            |>Array.filter (fun x-> not(ignoreList|>Array.contains x.Path) ) 
            //TODO: this may have to be changed becuase ignorelist now takes ino account fileinfo like acessedtime
        fileNames
    
    ///Returns the filePaths of files not part of the ignorelist
    let checkForNewFiles2 (ignoreList: string[]) (folder) =
       
        DirectoryInfo( folder).GetFiles()|>Array.map(fun i-> { Path=i.FullName;FTPFileInfo=None}) |>Array.filter (fun x-> not(ignoreList|>Array.contains x.Path) ) 
        
    

    let ActionNewFiles2 dbAcess (watchDir:WatchDir)   =
        (asyncSeq{ 
            let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
            use nullableFTPConnection= 
                match watchDir.MovementData.SourceFTPData with 
                |Some x->
                    Logging.infof "{Watcher} Connecting to ftp:%A"x
                    let con= new FtpClient(x.Host,x.User,x.Password)
                    con.Connect()
                    con
                |None -> 
                    null

            while true do
                let newFilesFunc=
                    match nullableFTPConnection with 
                    |null-> checkForNewFiles2 
                    |con->checkForNewFilesFTP con 
                let newFiles=newFilesFunc ignoreList watchDir.MovementData.DirData.SourceDir   
                for file in newFiles do
                    let extension= (Path.GetExtension file.Path)
                    let transcode= 
                        match watchDir.MovementData.TranscodeData with
                        |Some x-> x.TranscodeExtensions|>List.contains extension
                        |None-> false
                    
                    let task = Scheduler.scheduleTransfer file watchDir.MovementData dbAcess transcode
                    Logging.infof "{Found} and created scheduled task for file %s" (Path.GetFileName file.Path)
                    yield task
                ignoreList<- ignoreList|> Array.append (newFiles|> Array.map(fun x->x.Path))
                do! Async.Sleep(500);
        },watchDir.MovementData.DirData.GroupName)

   
    let GetNewTransfers2 watchDirs dbAccess=
        let tasks=watchDirs|>List.map (ActionNewFiles2 dbAccess)
        tasks