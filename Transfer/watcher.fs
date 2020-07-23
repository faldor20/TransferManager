namespace Transfer

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open Mover
open FluentFTP
open Data.Types;
open FSharp.Control
open FSharp.Control.Reactive.Builders
module Watcher =
    
//======================
// Here we have the new version of the scheduling code that uses async sequences
//====================

    let checkForNewFiles2 (ignoreList: string []) (folder) =
        
        DirectoryInfo( folder).GetFiles()|>Array.map(fun i-> i.FullName)|> Array.except ignoreList
    

    let ActionNewFiles2 (watchDir:WatchDir) =
        (asyncSeq{ 
            let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
            while true do
                let newFiles=checkForNewFiles2 ignoreList watchDir.MovementData.DirData.SourceDir
                for file in newFiles do
                    let extension= (Path.GetExtension file)
                    let transcode= 
                        match watchDir.MovementData.TranscodeData with
                        |Some x-> x.TranscodeExtensions|>List.contains extension
                        |None-> false
                    
                    let task = Scheduler.scheduleTransfer file watchDir.MovementData transcode
                    printfn "Created schedule task for file %s" (Path.GetFileName file)
                    yield task
                ignoreList<- ignoreList|> Array.append newFiles
                do! Async.Sleep(500);
        },watchDir.MovementData.DirData.GroupName)

   
    let GetNewTransfers2 watchDirs=
        let tasks=watchDirs|>List.map ActionNewFiles2
        tasks