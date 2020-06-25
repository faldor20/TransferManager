namespace Transfer

open System.IO
open System.Threading.Tasks
open Mover
open FluentFTP
module Watcher =
(* type WatchDir=
        |FTPWatchDir of WatchDir *FluentFTP.FtpClient
        |FileWatchDir of WatchDir *)
    type WatchDir =
        { Dir: DirectoryInfo
          OutPutDir: string
          TransferedList: string list
          IsFTP:bool;}
    type WatchDirSimple = { Source: string; Destination: string; IsFTP:bool; }




    let checkForNewFiles (ignoreList: string list) (files: FileInfo array) =
        //this ca be made way faster by sorting both lists and crossing them off as you go
        //or i could jsut delet the file nonce it is transfered
        //i allso need a way to cull the old list if a file is deleted
        files
        |> Array.filter (fun file -> not (ignoreList |> List.contains file.Name))

    let ActionNewFiles (newFilesForEachWatchDir:(FileInfo []*WatchDir)list) =
        //We iterate through the list each pair contains watchdir and a list of the new files in that dir
        let transfers =newFilesForEachWatchDir|> List.map (fun (files,watchDir) ->
            //we iterate through the files creating a new movejob for each in turn returning the task and the filename of the file it is for
            let tasksAndFiles= files|>Array.toList|> List.map(fun file -> 
                let handler data (guid) =
                    if Data.data.ContainsKey guid then (Data.setTransferData data guid) else guid|>Data.setTransferData data 
                (MoveFile watchDir.IsFTP watchDir.OutPutDir file.FullName (System.Guid.NewGuid()) handler,file.Name) 
            )
            
            let tasks,processedFiles=tasksAndFiles|>List.unzip
            //Return the tasks and a watchDir that includes the newly actioned files in its ignoreList
            tasks,{ watchDir with TransferedList = watchDir.TransferedList @ (processedFiles ) }
        )
        //seperate out the tasks and watchdirs
        let transferTasks, updatedwatchDirs = List.unzip transfers
        transferTasks,updatedwatchDirs

        
    let iterFolders (watchDirs: WatchDir list) =
        watchDirs|> List.map (fun folder ->
            let newFiles=folder.Dir.GetFiles()
                        |> checkForNewFiles folder.TransferedList
            (newFiles,folder)
            )
        

    let GetNewTransfers watchDirs=
        let unActionedFiles= iterFolders watchDirs
        let transfers= ActionNewFiles unActionedFiles ;
        transfers
    
    (*   let iterFolders2 (folders:WatchDir list)=
        folders|>List.map (fun folder->
           folder.Dir.GetFiles()|> ) *)
