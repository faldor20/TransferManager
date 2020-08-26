namespace TransferClient.IO
open System.Runtime
open System
open System.IO
open System.Threading
open System.Diagnostics
open System.Threading.Tasks
open FSharp.Control.Tasks
open SharedFs.SharedTypes;
open FluentFTP
open ProgressHandlers
open TransferClient.DataBase.Types
open FileMove
open Types
open TransferClient
module Mover =
    
    let ftpResToTransRes (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        |_-> failwith "ftpresult return unhandled enum value"
    let runFtp ftpData sourceStream destFilePath callBack (ct:CancellationToken)=async{
        use client=new FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
        client.Connect()
        Logging.verbosef "{Mover} Opening FTP writeStream for file %s" destFilePath
        use destStream= client.OpenWrite(destFilePath,FtpDataType.Binary)
        try 
            let writeResult=FileMove.writeWithProgress sourceStream destStream callBack (int (Math.Pow( 2.0,19.0))) ct
            sourceStream.Close()
            destStream.Close()
            let reply=client.GetReply()

            //We need to check the ftp status code returned after the write completed
            let res=
                if not reply.Success then
                    Logging.errorf "{Mover} Ftp writer returned failure: %A"reply
                    TransferResult.Failed
                else
                    writeResult
            let out =
                if (res = TransferResult.Cancelled
                    || res = TransferResult.Failed) then
                    try
                        Logging.warnf "{Mover} Cancelled or failed. Deleting ftp file %s" destFilePath
                        client.DeleteFile destFilePath
                        res
                    with _ ->
                        Logging.errorf "{Mover} Cancelled or failed and was unable to delete output ftp file %s" destFilePath
                        TransferResult.Failed
                else
                    res
            return out
            
        with 
        | :? OperationCanceledException-> return TransferResult.Cancelled 
    }


    let MoveFile (sourceFilePath:string) moveData dbAccess transcode (ct:CancellationTokenSource) =
 
        let {DestinationDir=destination; GroupName= groupName}=moveData.DirData
        //let isFTP=moveData.FTPData.IsSome
        
        
        let fileName= Path.GetFileName sourceFilePath
        
        //I could go two ways with the input:
        //1. i could use descriminated unions to have either a stream input or a file input depending on whether it is local or ftp.
        //2. i could allways feed in a stream input and just open a filestream for local files
        let mutable sourceClient=None;
        use inputStream=
            match moveData.SourceFTPData with
                |Some sourceFTPData->
                    let client=new FluentFTP.FtpClient(sourceFTPData.Host,sourceFTPData.User,sourceFTPData.Password)
                    client.Connect()
                    sourceClient<-Some client
                    Logging.verbosef "{Mover} Opening FTP readStream for file %s" sourceFilePath
                    client.OpenRead(sourceFilePath,FtpDataType.Binary,true)
                    
                |None->
                    Logging.verbosef "{Mover} Opening Read FileStream for file %s" sourceFilePath
                    let stream=new FileStream(sourceFilePath,FileMode.Open,FileAccess.Read)
                    stream :> Stream
        
        let doMove progressHandler=async{
                let result= 
                    //The particular transfer action to take has allready been decided by the progress callback
                    match progressHandler with
                        |FtpProg (cb,ftpData)           -> runFtp ftpData inputStream (destination+fileName) cb ct.Token
                        |FastFileProg cb                -> SCopy inputStream fileName destination cb ct.Token
                        //|TranscodeProg (cb, ffmpegInfo) -> VideoMover.Transcode ffmpegInfo moveData.FTPData cb filePath destination ct.Token
                return! result 
            }

        async {
            // We pass this in to the progressCallback so it  
            let transData= {dbAccess.Get() with StartTime=DateTime.Now}
            
            let newDataHandler newTransData=
                
                dbAccess.Set newTransData

            let progressHandler= Gethandler moveData transcode transData newDataHandler

            let transType=
                match progressHandler with 
                |FtpProg _-> "FTP Transfer"
                |FastFileProg _->"File transfer"
                |TranscodeProg _-> "FFmpeg transcode"
            Logging.infof " {Starting} %s from %s to %s" transType sourceFilePath destination
           
            //We have to set the startTime here because we want the sartime to truly be when the task begins
            dbAccess.Set {transData with StartTime=DateTime.Now}
            
            let! result= doMove progressHandler
            //We need to dispose the sourceclient if there is one. If we getrid of this we would endlessly increase our number of active connections
            match sourceClient with|Some x-> x.Dispose()|None->()

            Logging.infof " {Finished} copy from %s to %s"sourceFilePath destination
            return (result,dbAccess,moveData.DirData.DeleteCompleted)
        }
