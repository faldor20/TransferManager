
module Mover.ProgressHandlers
open System
open System.Diagnostics
open Types 
open SharedFs.SharedTypes;
open FluentFTP
open FFmpeg.NET
///The **TimeSpan** is the duration of the source clip
///The **ConversionProgressEventArgs** is what it sounds like :)
type TranscodeProgress= (TimeSpan->Events.ConversionProgressEventArgs ->unit)
type ProgressHandler=
    | DoubleFtpProg of (FileMove.ProgressData->unit)*FTPData
    | FtpProg of Progress<FtpProgress>
    | FastFileProg of (FileMove.ProgressData->unit)
    | TranscodeProg of TranscodeProgress*TranscodeData
    /// A function that will be called whenever new transfer data is available.
    /// It will most likely be used to write to console or into a database
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
                            FileRemaining=double((fileSize- transferred)/int64 1000)/ 1000.0
                            EndTime=DateTime.Now
                            Status=TransferStatus.Copying}
        newDataHandler lastTransferData 
            

    let fastFileProgress  = (fun (progress:FileMove.ProgressData )->
        setData progress.Progress progress.BytesTransfered
        )
               
    let newftpProgress=(fun (progress:FileMove.ProgressData )->
        setData progress.Progress progress.BytesTransfered
    )
    let ftpProgress:Progress<FtpProgress>  =new Progress<FtpProgress>(fun prog ->
                if stopWatch.ElapsedMilliseconds>int64 500 then 
                    setData prog.Progress prog.TransferredBytes
    )
    let transcodeProgress (sourceDuration:TimeSpan)(eventArgs:Events.ConversionProgressEventArgs) = 
        if stopWatch.ElapsedMilliseconds>int64 500 then
            let KBrate= match  Option.ofNullable eventArgs.Bitrate with | Some x-> x / 8.0|None->0.0
            let MBrate= KBrate/1000.0
            //this means MB/s*speed multiplyer(frames per secondof video/number of frames being processed each second)
            let speed= if eventArgs.Fps.HasValue then (MBrate *(eventArgs.Fps.Value/24.0)) else 0.0

            //let size= if eventArgs.SizeKb.HasValue then float eventArgs.SizeKb.Value/1000.0 else fileSizeMB 
            //we have to sue the expected size beuase otherwise the eta will be wrong.
            //TODO: we could just ahve a different data structutre for transcode jobs and disaly differnet info in the ui.(that would be scary)
            let expectedSize= (sourceDuration.TotalSeconds*MBrate)
                
            let remaining= MBrate * float (sourceDuration- eventArgs.ProcessedDuration).TotalSeconds
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
                    FileSize= expectedSize
                    Status=TransferStatus.Copying
                }
            newDataHandler lastTransferData 
                
    //TODO: put another option here for transcode without ftp
    if transcode then (TranscodeProg (transcodeProgress, moveData.TranscodeData.Value))
    else if moveData.SourceFTPData.IsSome then FtpProg (ftpProgress)
    else if moveData.DestFTPData.IsSome then FtpProg (ftpProgress)
    else (FastFileProg fastFileProgress)
         