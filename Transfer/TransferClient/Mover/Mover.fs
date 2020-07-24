namespace TransferClient
open System.Runtime
open System
open System.IO
open System.Threading
open System.Diagnostics
open IOExtensions
open ClientManager.Data.Types
open System.Threading.Tasks
open FSharp.Control.Tasks
open SharedFs.SharedTypes;
open FluentFTP
open ProgressHandlers
module Mover =
    let TransferResult (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        |_-> failwith "ftpresult return unhandled enum value"
        
    let MoveFile (filePath:string) moveData index transcode (ct:CancellationTokenSource) =
        
        let {DestinationDir=destination; GroupName= groupName}=moveData.DirData
        let isFTP=moveData.FTPData.IsSome
        let progressCallback= Gethandler moveData filePath transcode index
        
        let fileName= Path.GetFileName filePath

        let task=async{
                let runFtp ftpData callBack=async{
                    use client=new FtpClient()
                    client.Connect()
                    let task= Async.AwaitTask(client.UploadFileAsync (filePath,(destination+fileName),FtpRemoteExists.Overwrite,false,FtpVerify.Throw,  callBack ,ct.Token ))
                    try 
                        let! a= task
                        return TransferResult a
                    with 
                    | :? OperationCanceledException-> return IOExtensions.TransferResult.Cancelled
                }
                
                let result= 
                    //The particular transfer action to take has allready been decided by the progress callback
                    match progressCallback with
                        |FtpProg (cb,ftpData)           -> runFtp ftpData cb
                        |FileProg cb                    -> Async.AwaitTask (FileTransferManager.CopyWithProgressAsync(filePath, destination, cb,false,ct.Token))
                        |TranscodeProg (cb, ffmpegInfo)  -> VideoMover.Transcode ffmpegInfo moveData.FTPData cb filePath destination ct.Token
                return! result 
            }
        async {
            let transType=
                match progressCallback with 
                |FtpProg-> "FTP Transfer"
                |FileProg->"File transfer"
                |TranscodeProg-> "FFmpeg transcode"
            
            printfn "starting %s from %s to %s" transType filePath destination
            let! result= task
            
            printfn "finished copy from %s to %s"filePath destination
            return (result,index)
        }
