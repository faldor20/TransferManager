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
open StackExchange.Profiling
open TransferClient
module Mover =
    
    let ftpResToTransRes (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        |_-> failwith "ftpresult return unhandled enum value"
   
    let MoveFile (filePath:string) moveData dbAccess transcode (ct:CancellationTokenSource) =
        let proflier = MiniProfiler.Current
        let {DestinationDir=destination; GroupName= groupName}=moveData.DirData
        let isFTP=moveData.FTPData.IsSome
        
        
        let fileName= Path.GetFileName filePath

        let task progressHandler=async{
                let runFtp ftpData callBack=async{
                    use client=new FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
                    client.Connect()
                    let task= 
                        Async.AwaitTask(
                            client.UploadFileAsync (
                                filePath,
                                (destination+fileName),
                                FtpRemoteExists.Overwrite,
                                false,
                                FtpVerify.Throw,
                                  callBack ,
                                  ct.Token ))
                    try 
                        let! a= task
                        return ftpResToTransRes a
                    with 
                    | :? OperationCanceledException-> return TransferResult.Cancelled 
                }
                
                let result= 
                    //The particular transfer action to take has allready been decided by the progress callback
                    match progressHandler with
                        |FtpProg (cb,ftpData)           -> runFtp ftpData cb
                        |FastFileProg cb                -> 
                            using (proflier.Step("fileMove")) (fun x ->
                            FCopy filePath destination cb ct.Token)
                        |TranscodeProg (cb, ffmpegInfo) -> VideoMover.Transcode ffmpegInfo moveData.FTPData cb filePath destination ct.Token
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
                |FtpProg-> "FTP Transfer"
                |FastFileProg->"File transfer"
                |TranscodeProg-> "FFmpeg transcode"
            Logging.infof " {Starting} %s from %s to %s" transType filePath destination
            using (proflier.Step("dbacess"))(fun x->
            //We have to set the startTime here because we want the sartime to truly be when the task begins
            dbAccess.Set {transData with StartTime=DateTime.Now}
            )
            let! result= task progressHandler
            printfn "%s" (proflier.RenderPlainText(false))
            Logging.infof " {Finished} copy from %s to %s"filePath destination
            return (result,dbAccess)
        }
