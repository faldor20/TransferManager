namespace Transfer

open System.IO
open System.Threading
open System
open Transfer.Data
open SharedFs.SharedTypes
module Scheduler =
    //This will return once the file is not beig acessed by other programs.
    //it returns false if the file is discovered to be deleted before that point.
    let isAvailable source =
        async {
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
                    | :? FileNotFoundException->
                        fileExists<-false
                        unavailable<-false
                    | :? IOException ->
                        do! Async.Sleep(1000)
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
    let scheduleTransfer groupName  isFTP destination source =
        async {
            
            let index= 
                addTransferData
                    { Percentage = 0.0
                      FileSize = float(FileInfo(source).Length/int64 1000/int64 1000)
                      FileRemaining = 0.0
                      Speed = 0.0
                      Destination = destination
                      Source = source
                      StartTime = DateTime.Now
                      ID = 0
                      GroupName=groupName
                      Status = TransferStatus.Waiting 
                      EndTime=DateTime.Now} 
                      groupName 
            
            
            let ct = new CancellationTokenSource()
            printfn "Scheduled transfer from %s To-> %s at index:%i" source destination index
            addCancellationToken groupName ct
            let! fileAvailable= isAvailable2 source
            if fileAvailable then
                printfn "Transfer file at: %s is available" source 
                return Mover.MoveFile isFTP destination source groupName index ct
            else
                printfn "Transfer file at: %s was deleted" source 
                setTransferData {dataBase.[groupName].[index] with Status=TransferStatus.Failed} groupName index
                return async{ return (IOExtensions.TransferResult.Failed,index)}
        }
