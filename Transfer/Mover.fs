namespace Transfer
open System.Runtime
open System
open System.IO
open System.Diagnostics
open IOExtensions
open Transfer.Data
open System.Threading.Tasks
open FSharp.Control.Tasks
open SharedData;
open FluentFTP
module Mover =
    let TransferResult (ftpResult:FtpStatus)=
        match ftpResult with
        |FtpStatus.Failed->TransferResult.Failed
        |FtpStatus.Success->TransferResult.Success
        
    let MoveFile isFTP destination source (guid:Guid) eventHandler =
        let stopWatch = new Stopwatch()
        
        let startTime= DateTime.Now

        setTransferData { Percentage = 0.0; FileSize=0.0; FileRemaining=0.0; Speed = 0.0;Destination = destination;Source = source; StartTime=startTime; id=guid; Status=TransferStatus.Waiting} guid
       
        let mutable lastTransfered = int64 0
        let fileSize=(new FileInfo(source)).Length
        let fileSizeMB=(float(fileSize/int64 1000))/1000.0
        stopWatch.Start()
        let outputStatsFileTrans  =Action<TransferProgress> (fun progress->
           
            if stopWatch.ElapsedMilliseconds>int64 500 then 
                let speed =(double(progress.Transferred- lastTransfered)/  double(1000*1000))/ (double stopWatch.ElapsedMilliseconds / double 1000)
                lastTransfered<-progress.Transferred
                stopWatch.Reset()
                stopWatch.Start()
                eventHandler    { Percentage = float (MathF.Round(float32 progress.Percentage,2))
                                  FileSize= float(progress.Total/ int64 1000 )/1000.0
                                  Speed = float (MathF.Round((float32 speed),2))
                                  FileRemaining=float((progress.Total- progress.Transferred)/int64 1000/int64 1000)
                                  Destination = destination
                                  Source = source
                                  id=guid
                                  StartTime=startTime
                                  Status=TransferStatus.Copying} guid
      
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
                                  Speed = float (MathF.Round((float32 speed),2))
                                  FileRemaining=float((fileSize- prog.TransferredBytes)/int64 1000/int64 1000)
                                  Destination = destination
                                  Source = source
                                  id=guid
                                  StartTime=startTime
                                  Status=TransferStatus.Copying} guid
      
                              )
        
        (* let timer = new System.Timers.Timer(updateSpeed)
        timer.AutoReset <- true
        timer.Elapsed.Add outputStats
 *)
       (*  let progressPrint =
            Action<TransferProgress>(fun prog -> progress <- prog) *)
        let ct = new Threading.CancellationTokenSource()
        Data.CancellationTokens.Add(guid,ct);
        
        let isAvailabe=
           async{ 
                let mutable currentFile = new FileInfo(source);
               //try replacing this with a while to stop the stack overflow
              (*   let rec waitTillAcessable ()=
                    currentFile.Refresh()
                    try 
                      //  currentFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                        using(currentFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None)) (fun stream-> stream.Close())
                    with
                        |_-> Task.Delay(500).Wait()
                             waitTillAcessable() 

                waitTillAcessable() *)
                let mutable unavailable=true
                while unavailable do
                    try 
                      //  currentFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                        using(currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream-> stream.Close())
                        unavailable<-false
                    with
                        |IOException->  Task.Delay(1000).Wait()
                                        unavailable<-true
                
            }

            
        let fileName= 
            let a=source.Split('\\')
            a.[a.Length-1]
        async {
           
               //TODO: this is a total hack and i dhouls be able to find a better way
            do! isAvailabe
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
