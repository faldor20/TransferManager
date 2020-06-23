namespace Transfer

open System.IO
open System.Threading.Tasks
open Mover

module Watcher =
    type WatchDir =
        { Dir: DirectoryInfo
          OutPutDir: DirectoryInfo
          TransferedList: string list }

    let MoveFile500Update = MoveFile 500.0




    let checkForNewFiles (ignoreList: string list) (files: FileInfo array) =
        //this ca be made way faster by sorting both lists and crossing them off as you go
        //or i could jsut delet the file nonce it is transfered
        //i allso need a way to cull the old list if a file is deleted
        files
        |> Array.filter (fun file -> not (ignoreList |> List.contains file.Name))

    let iterFolders (watchDir: WatchDir list) =
        let transfers =watchDir|> List.map (fun folder ->
            let newFiles =folder.Dir.GetFiles()
                        |> checkForNewFiles folder.TransferedList

            let transfers =newFiles|> Array.map (fun file ->
                    let handler data (guid) =if Data.data.ContainsKey guid then (Data.setTransferData data guid) else guid|>Data.setTransferData data 
                    
                    (MoveFile500Update  folder.OutPutDir.FullName file.FullName (System.Guid.NewGuid()) handler,file.Name)
               
                    )
            let transferTasks, newWatchDirs = Array.unzip transfers

            transferTasks,{ folder with TransferedList = folder.TransferedList @ (newWatchDirs |> Array.toList) }
            )
        transfers
    (*   let iterFolders2 (folders:WatchDir list)=
        folders|>List.map (fun folder->
           folder.Dir.GetFiles()|> ) *)
