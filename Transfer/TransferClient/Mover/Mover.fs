namespace TransferClient.IO
open System.Runtime
open System
open System.IO
open System.Threading
open System.Diagnostics
open ClientManager.Data.Types
open System.Threading.Tasks
open FSharp.Control.Tasks
open SharedFs.SharedTypes;
open FluentFTP
open ProgressHandlers
open TransferClient.DataBase.Types
open FileMove
open Types
module Mover =
    
    let ftpResToTransRes (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        |_-> failwith "ftpresult return unhandled enum value"
   
    let MoveFile (filePath:string) moveData dbAccess transcode (ct:CancellationTokenSource) =
        
        let {DestinationDir=destination; GroupName= groupName}=moveData.DirData
        let isFTP=moveData.FTPData.IsSome
        
        
        let fileName= Path.GetFileName filePath

        let task progressHandler=async{
                let runFtp ftpData callBack=async{
                    use client=new FtpClient()
                    client.Connect()
                    let task= Async.AwaitTask(client.UploadFileAsync (filePath,(destination+fileName),FtpRemoteExists.Overwrite,false,FtpVerify.Throw,  callBack ,ct.Token ))
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
                        |FastFileProg cb                -> FCopy filePath destination cb ct.Token
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
                |FileProg->"File transfer"
                |TranscodeProg-> "FFmpeg transcode"
            printfn "starting %s from %s to %s" transType filePath destination

            //We have to set the startTime here because we want the sartime to truly be when the task begins
            dbAccess.Set {transData with StartTime=DateTime.Now}
            
            let! result= task progressHandler
            
            printfn "finished copy from %s to %s"filePath destination
            return (result,dbAccess)
        }
