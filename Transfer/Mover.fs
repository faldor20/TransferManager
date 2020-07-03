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
module Mover =
    let TransferResult (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        
    let MoveFile isFTP destination source groupName (guid:int) eventHandler (ct:CancellationTokenSource) =
        let stopWatch = new Stopwatch()
        
        let startTime= DateTime.Now

        
       
        let mutable lastTransfered = int64 0
        let mutable fileSize=(new FileInfo(source)).Length;
        let mutable fileSizeMB=(float(fileSize/int64 1000))/1000.0

        stopWatch.Start()
        let outputStatsFileTrans  =Action<TransferProgress> (fun progress->
           
            if stopWatch.ElapsedMilliseconds>int64 500 then 
                let speed =(double(progress.Transferred- lastTransfered)/  double(1000*1000))/ (double stopWatch.ElapsedMilliseconds / double 1000)
                lastTransfered<-progress.Transferred
                stopWatch.Reset()
                stopWatch.Start()
                eventHandler    { Percentage = float  progress.Percentage
                                  FileSize= float(progress.Total/ int64 1000 )/1000.0
                                  Speed = float  speed
                                  FileRemaining=float((progress.Total- progress.Transferred)/int64 1000/int64 1000)
                                  Destination = destination
                                  Source = source
                                  id=guid
                                  StartTime=startTime
                                  Status=TransferStatus.Copying
                                  EndTime=DateTime.Now} groupName guid
                              )
        let ftpProgress:Progress<FtpProgress>  =new Progress<FtpProgress>(fun prog ->
            if stopWatch.ElapsedMilliseconds>int64 500 then 
                let speed =(double(prog.TransferredBytes- lastTransfered)/  double(1000*1000))/ (double stopWatch.ElapsedMilliseconds / double 1000)
                //let speed =prog.TransferSpeed/1000.0/1000.0
                lastTransfered<-prog.TransferredBytes
                stopWatch.Reset()
                stopWatch.Start()
                eventHandler    { Percentage = prog.Progress
                                  FileSize= fileSizeMB
                                  Speed = float speed
                                  FileRemaining=float((fileSize- prog.TransferredBytes)/int64 1000/int64 1000)
                                  Destination = destination
                                  Source = source
                                  id=guid
                                  StartTime=startTime
                                  Status=TransferStatus.Copying
                                  EndTime=DateTime.Now} groupName guid
      
                              )
        
        let fileName= 
            let a=source.Split('\\')
            a.[a.Length-1]
        async {
           //THIS CODE SHOULD NOWBE UNECISSARY BECUASE THE SCHEDULER MAKES SURE THE FILE HAS COMPLETED TRANSFERINGBEFORE THE TASK IS BEGUN
        (*     fileSize<-(new FileInfo(source)).Length;
            fileSizeMB<-(float(fileSize/int64 1000))/1000.0 *)
            let task=async{
                
                
                let runFtp=async{
                    let ip::path=destination.Split '@'|>Array.toList
                    use client=new FluentFTP.FtpClient( ip,21,"quantel","***REMOVED***")
                    client.Connect()
                    let task= Async.AwaitTask(client.UploadFileAsync (source,(path.[0]+fileName),FtpRemoteExists.Overwrite,false,FtpVerify.Throw,ftpProgress ,ct.Token ))
                    try 
                        let! a= task
                        return TransferResult a
                    with 
                    | :? OperationCanceledException-> return IOExtensions.TransferResult.Cancelled
                                
                }
                let result= 
                    match isFTP with
                        |true-> runFtp
                        |false->  Async.AwaitTask (FileTransferManager.CopyWithProgressAsync(source, destination, outputStatsFileTrans,false,ct.Token))
                return! result 
                
            }
            printfn "starting copy from %s to %s"source destination
            let! result= task
            printfn "finished copy from %s to %s"source destination
            return (result,guid,ct)
            
        }
