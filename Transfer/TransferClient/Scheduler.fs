namespace TransferClient

open System.IO
open System.Threading
open System
open ClientManager.Data.Types
open SharedFs.SharedTypes
open ClientManager.Data.TokenDatabase
open SignalR.ManagerCalls
module Scheduler =
    //This will return once the file is not beig acessed by other programs.
    //it returns false if the file is discovered to be deleted before that point.
    let isAvailable (source:string) =
        async {
            let fileName= source.Split("\\")|>Array.last
            let mutable currentFile = new FileInfo(source)
            let mutable unavailable = true
            let mutable fileExists=false
            while unavailable do
                try
                    using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                        stream.Close())
                    fileExists<-true
                    unavailable<-false
                with 
                    | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                        printfn "%s deleted while waiting to be available" fileName
                        fileExists<-false
                        unavailable<-false
                    | :? IOException ->
                        do! Async.Sleep(1000)
                    | ex  ->
                        printfn "file failed with %A" ex.Message
                        unavailable<- false
                        fileExists<-false
            return fileExists
        }
        //i think this has some kind of overflow still
    let isAvailable2 source=
        async{
            let rec loop (currentFile:FileInfo)  = 
                async {
                    do! Async.Sleep(1000)
                    try
                        using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                            stream.Close())
                        return true
                    with 
                        | :? FileNotFoundException->
                             return false
                        | :? IOException ->
                            do! Async.Sleep(1000)
                            return! loop(currentFile)
                        |_->failwith "something went very wrong while waiting for a file to become available"
                            return false
                }
            return! loop(FileInfo(source))
        }
    let scheduleTransfer filePath moveData transcode =
        async {
            let {DestinationDir=dest;GroupName=groupName}:DirectoryData=moveData.DirData
            let data=
                { Percentage = 0.0
                  FileSize = float(FileInfo(filePath).Length/int64 1000/int64 1000)
                  FileRemaining = 0.0
                  Speed = 0.0
                  Destination = dest
                  Source = filePath
                  StartTime = new DateTime()
                  ID = 0
                  GroupName=groupName
                  Status = TransferStatus.Waiting 
                  EndTime=new DateTime()}
            let index= 
                addTransferData data groupName 
            
            
            let ct = new CancellationTokenSource()
            let transType=
                ""  |>fun s->if transcode then s+" transcode"else s
                    |>fun s->if moveData.FTPData.IsSome then s+" ftp" else s
            printfn "Scheduled%s transfer from %s To-> %s at index:%i" transType filePath dest index
            addCancellationToken groupName index ct
            let! fileAvailable= isAvailable filePath
            if fileAvailable then
                printfn "Transfer file at: %s is available" filePath
                return Mover.MoveFile filePath moveData index transcode  ct
            else
                printfn "Transfer file at: %s was deleted" filePath 
                setTransferData {data with Status=TransferStatus.Failed} groupName index
                return async{ return (IOExtensions.TransferResult.Failed,index)}
        }
