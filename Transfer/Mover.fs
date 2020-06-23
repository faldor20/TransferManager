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
module Mover =
    let MoveFile updateSpeed destination source (guid:Guid) eventHandler =
        let stopWatch = new Stopwatch()
        
        let startTime= DateTime.Now

        setTransferData { Percentage = 0.0; FileSize=0.0; FileRemaining=0.0; Speed = 0.0;Destination = destination;Source = source; StartTime=startTime; id=guid; Status=TransferStatus.Waiting} guid
       
        let mutable lastTransfered = int64 0
     
        stopWatch.Start()
        let outputStats  =Action<TransferProgress> (fun progress->
           
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
                        |_->  Task.Delay(1000).Wait()
                              unavailable<-true
                
            }

            
            
        async {
           
               //TODO: this is a total hack and i dhouls be able to find a better way
               
            let task=async{
                do! isAvailabe
                let task= FileTransferManager.CopyWithProgressAsync(source, destination, outputStats,false,ct.Token)
                return! Async.AwaitTask task
            }
            printfn "starting copy from %s to %s"source destination
            let! result= task

            return (result,guid,ct)
            
        }
