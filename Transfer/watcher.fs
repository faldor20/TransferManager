namespace Transfer

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open Mover
open FluentFTP
open Data;
open FSharp.Control
module Watcher =
    let handler data (guid) =
        if Data.data.ContainsKey guid then (Data.setTransferData data guid) else guid|>Data.setTransferData data 
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