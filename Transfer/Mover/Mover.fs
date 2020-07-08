namespace Transfer
open System.Runtime
open System
open System.IO
open System.Threading
open System.Diagnostics
open IOExtensions
open Transfer.Data
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
        
    let MoveFile isFTP (destination:string) source groupName (guid:int) (ct:CancellationTokenSource) =
        let progressCallback= Gethandler isFTP destination source groupName guid
        
        let fileName= 
            let a=source.Split('\\')
            a.[a.Length-1]

        let task=async{
                let runFtp callBack=async{
                    let ip::path=destination.Split '@'|>Array.toList
                    use client=new FtpClient( ip,21,"quantel","***REMOVED***")
                    client.Connect()
                    let task= Async.AwaitTask(client.UploadFileAsync (source,(path.[0]+fileName),FtpRemoteExists.Overwrite,false,FtpVerify.Throw,  callBack   ,ct.Token ))
                    try 
                        let! a= task
                        return TransferResult a
                    with 
                    | :? OperationCanceledException-> return IOExtensions.TransferResult.Cancelled
                }
                
                let result= 
                    match progressCallback with
                        |FtpProg cb-> runFtp cb
                        |FileProg cb->  Async.AwaitTask (FileTransferManager.CopyWithProgressAsync(source, destination, cb,false,ct.Token))
                return! result 
            }
        async {
            printfn "starting copy from %s to %s"source destination
            let! result= task
            printfn "finished copy from %s to %s"source destination
            return (result,guid)
        }
