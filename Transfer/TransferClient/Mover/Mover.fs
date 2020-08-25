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
    let runFtp ftpData sourceStream destination fileName callBack (ct:CancellationToken)=async{
        use client=new FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
        client.Connect()
        let destStream= client.OpenWrite((destination+fileName),FtpDataType.ASCII)
        try 
            let res=FileMove.writeWithProgress sourceStream destStream callBack (int (Math.Pow( 2.0,19.0))) ct
            sourceStream.Close()
            destStream.Close()
            let out =
                if (res = TransferResult.Cancelled
                    || res = TransferResult.Failed) then
                    try
                        printfn "Cancelled or failed deleting ftp file %s" (destination+fileName)
                        client.DeleteFile(destination+fileName)
                        res
                    with _ ->
                        printfn "Cancelled or failed and was unable to delete output ftp file %s" (destination+fileName)
                        TransferResult.Failed
                else
                    res
            return out
            
        with 
        | :? OperationCanceledException-> return TransferResult.Cancelled 
    }


    let MoveFile (filePath:string) moveData dbAccess transcode (ct:CancellationTokenSource) =
 
        let {DestinationDir=destination; GroupName= groupName}=moveData.DirData
        //let isFTP=moveData.FTPData.IsSome
        
        
        let fileName= Path.GetFileName filePath
        
        //I could go two ways with the input:
        //1. i could use descriminated unions to have either a stream input or a file input depending on whether it is local or ftp.
        //2. i could allways feed in a stream input and just open a filestream for local files
        let inputStream=
            let sourceDir=moveData.DirData.SourceDir
            match moveData.SourceFTPData with
                |Some sourceFTPData->
                    let client=new FluentFTP.FtpClient(sourceFTPData.Host,sourceFTPData.User,sourceFTPData.Password)
                    client.Connect()
                    client.OpenRead(sourceDir)
                |None->
                    let stream=new FileStream(sourceDir,FileMode.Open,FileAccess.Read)
                    stream :> Stream
        
        let doMove progressHandler=async{
                let result= 
                    //The particular transfer action to take has allready been decided by the progress callback
                    match progressHandler with
                        |FtpProg (cb,ftpData)           -> runFtp ftpData inputStream destination fileName cb ct.Token
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
            Logging.infof " {Starting} %s from %s to %s" transType filePath destination
           
            //We have to set the startTime here because we want the sartime to truly be when the task begins
            dbAccess.Set {transData with StartTime=DateTime.Now}
            
            let! result= doMove progressHandler
            
            Logging.infof " {Finished} copy from %s to %s"filePath destination
            return (result,dbAccess,moveData.DirData.DeleteCompleted)
        }
