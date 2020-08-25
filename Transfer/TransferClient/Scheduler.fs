namespace TransferClient

open System.IO
open System.Threading
open System
open TransferClient.DataBase.TokenDatabase
open TransferClient.IO.Types
open SharedFs.SharedTypes
open DataBase.Types
open Logary
open TransferClient.IO
module Scheduler =
    //This will return once the file is not beig acessed by other programs.
    //it returns false if the file is discovered to be deleted before that point.
    let isAvailable (source:string) =
        async {
            let fileName= Path.GetFileName (string source)
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
    let getFileData filePath currentTransferData=
        
        let fileSize=(new FileInfo(filePath)).Length;
        let fileSizeMB=(float(fileSize/int64 1000))/1000.0

        {currentTransferData with
                FileSize=fileSizeMB
        }
     
    let scheduleTransfer (filePath) moveData (dbAccess:DataBase.Types.DataBaseAcessFuncs) transcode =
        async {
            //this is only used for logging
            let logFilePath=match moveData.SourceFTPData with | Some _-> "FTP:"+filePath |None -> string filePath
            let {DestinationDir=dest;GroupName=groupName}:DirectoryData=moveData.DirData
            let transData=
                { Percentage = 0.0
                  FileSize = 0.0
                  FileRemaining = 0.0
                  Speed = 0.0
                  Destination = dest
                  Source =  filePath
                  StartTime =  DateTime.Now
                  ID = 0
                  GroupName=groupName
                  Status = TransferStatus.Waiting 
                  ScheduledTime=DateTime.Now
                  EndTime=new DateTime()}
            let index= dbAccess.Add  groupName transData

            let ct = new CancellationTokenSource()
            let transType=
                ""  |>fun s->if transcode then s+" transcode"else s
                    |>fun s->if moveData.SourceFTPData.IsSome||moveData.DestFTPData.IsSome then s+" ftp" else s
            Logging.infof "{Scheduled} %s  transfer from %s To-> %s at index:%i" transType logFilePath dest index
            addCancellationToken groupName index ct
            //This should only be run if reading from growing files is disabled otherwise ignroe it.
            //Doesn't work on ftp files
            let fileAvailable=
                match moveData.SourceFTPData with
                    |Some _-> true
                    |None-> Async.RunSynchronously( isAvailable filePath)
        
            let transDataAccess= TransDataAcessFuncs dbAccess groupName index

            if fileAvailable then
                Logging.infof "{Available} file at: %s is available" logFilePath 
                dbAccess.Set groupName index (getFileData filePath (dbAccess.Get groupName index) )  
                
                return Mover.MoveFile filePath moveData transDataAccess transcode  ct
            else
                Logging.warnf "{Deleted} While waiting to be available Transfer file at: %s" logFilePath 
                dbAccess.Set groupName index {dbAccess.Get groupName index with Status=TransferStatus.Failed} 
                return async{ return (Types.TransferResult.Failed, transDataAccess,moveData.DirData.DeleteCompleted)}
        }
