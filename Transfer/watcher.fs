namespace Transfer

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open Mover
open FluentFTP
open Data;
open FSharp.Control
module Watcher =
(* type WatchDir=
        |FTPWatchDir of WatchDir *FluentFTP.FtpClient
        |FileWatchDir of WatchDir *)
    




    let checkForNewFiles (ignoreList: string list) (files: FileInfo array) =
        //this ca be made way faster by sorting both lists and crossing them off as you go
        //or i could jsut delet the file nonce it is transfered
        //i allso need a way to cull the old list if a file is deleted
        files
        |> Array.filter (fun file -> not (ignoreList |> List.contains file.Name))
    let handler data (guid) =
        if Data.data.ContainsKey guid then (Data.setTransferData data guid) else guid|>Data.setTransferData data 

    let ActionNewFiles (newFilesForEachWatchDir:(FileInfo []*WatchDir)list) =
        //We iterate through the list each pair contains watchdir and a list of the new files in that dir
        let transfers =newFilesForEachWatchDir|> List.map (fun (files,watchDir) ->
            //we iterate through the files creating a new movejob for each in turn returning the task and the filename of the file it is for
            let tasks=asyncSeq{ 
                for file in files do
                    let task= (Scheduler.scheduleTransfer watchDir.IsFTP watchDir.OutPutDir file.FullName (System.Guid.NewGuid()) handler)
                    yield task
            }
            
            let processedFiles=files|>Array.map(fun item->item.Name)|>Array.toList
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
    
    
//======================
// Here we have the new version of the scheduling code that uses async sequences
//====================



    let checkForNewFiles2 (ignoreList: string []) (folder:DirectoryInfo) =
        folder.GetFiles()|>Array.map(fun i-> i.FullName)|> Array.except ignoreList
    

    let ActionNewFiles2 (watchDir:WatchDir) =
        asyncSeq{ 
                let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
                while true do
                let newFiles=checkForNewFiles2 ignoreList watchDir.Dir
                for file in newFiles do
                    let task = (Scheduler.scheduleTransfer watchDir.IsFTP watchDir.OutPutDir file (System.Guid.NewGuid()) handler)
                    yield task
                ignoreList<- ignoreList|> Array.append newFiles
                do! Async.Sleep(500);
        }

    let GetNewTransfers2 watchDirs=
        let tasks=watchDirs|>List.map ActionNewFiles2
        tasks