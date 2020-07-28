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
open TransferClient.LocalDB
open FluentFTP
open FFmpeg.NET
module ProgressHandlers=
    type ProgressHandler=
        | FtpProg of Progress<FtpProgress>*FTPData
        | FileProg of Action<TransferProgress>
        | TranscodeProg of (TimeSpan->Events.ConversionProgressEventArgs ->unit)*TranscodeData
    type NewDataHandler= TransferData->unit
    let Gethandler moveData transcode currentTransData newDataHandler =    
        let stopWatch =  Stopwatch()
       
        let mutable lastTransferData= currentTransData
        let fileSizeMB= lastTransferData.FileSize
        let fileSize= int64(lastTransferData.FileSize *1000.0*1000.0)
        let mutable lastTransfered = int64 0

        stopWatch.Start()

        let setData  percentage transferred =
            let speed =(double(transferred- lastTransfered)/  double(1000*1000))/ (double stopWatch.ElapsedMilliseconds / double 1000)
            lastTransfered<-transferred
            stopWatch.Reset()
            stopWatch.Start()
            lastTransferData<-
                        {lastTransferData with 
                                Percentage = percentage
                                Speed = float speed
                                FileRemaining=float((fileSize- transferred)/int64 1000/int64 1000)
                                EndTime=DateTime.Now
                                Status=TransferStatus.Copying}
            newDataHandler lastTransferData 
            

        let outputStatsFileTrans  =Action<TransferProgress> (fun progress->
            if stopWatch.ElapsedMilliseconds>int64 500 then 
               setData (float progress.Percentage) progress.BytesTransferred
               )

        let ftpProgress:Progress<FtpProgress>  =new Progress<FtpProgress>(fun prog ->
            if stopWatch.ElapsedMilliseconds>int64 500 then 
                setData prog.Progress prog.TransferredBytes
            )

        let transcodeProgress sourceDuration (eventArgs:Events.ConversionProgressEventArgs) = 
            if stopWatch.ElapsedMilliseconds>int64 500 then
                let byterate= (float eventArgs.Bitrate)/8.0
                let speed= if eventArgs.Fps.HasValue then ((byterate/1000.0) *(eventArgs.Fps.Value/24.0)) else 0.0
                let size= if eventArgs.SizeKb.HasValue then float eventArgs.SizeKb.Value/1000.0 else fileSizeMB 
                let remaining= byterate * float (sourceDuration- eventArgs.ProcessedDuration).Seconds
                //printfn "transferData for %s: %A"(Path.GetFileName filePath) eventArgs
                stopWatch.Reset()
                stopWatch.Start()
                lastTransferData<-
                    {
                    (lastTransferData) with 
                        Speed=speed
                        Percentage= (eventArgs.ProcessedDuration/sourceDuration)*100.0
                        EndTime=DateTime.Now
                        FileRemaining= remaining
                        FileSize= size
                        Status=TransferStatus.Copying
                    }
                newDataHandler lastTransferData 
                
        //TODO: put another option here for transcode without ftp
        if transcode then (TranscodeProg (transcodeProgress, moveData.TranscodeData.Value))
        else if moveData.FTPData.IsSome then FtpProg (ftpProgress, moveData.FTPData.Value)
        else (FileProg outputStatsFileTrans)
         