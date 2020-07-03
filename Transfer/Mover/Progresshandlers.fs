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
module ProgressHandlers=
    type ProgressHandlers=
        | FtpProg of Progress<FtpProgress>
        | FileProg of Action<TransferProgress>
    let Gethandler isFtp destination source groupName (index:int) =    
        let stopWatch = new Stopwatch()
            
        let startTime= DateTime.Now
        let mutable lastTransfered = int64 0
        let mutable fileSize=(new FileInfo(source)).Length;
        let mutable fileSizeMB=(float(fileSize/int64 1000))/1000.0
        stopWatch.Start()

        let setData  percentage transferred =
            let speed =(double(transferred- lastTransfered)/  double(1000*1000))/ (double stopWatch.ElapsedMilliseconds / double 1000)
            lastTransfered<-transferred
            stopWatch.Reset()
            stopWatch.Start()
            (groupName, index)||>setTransferData 
                        {dataBase.[groupName].[index] with 
                                Percentage = percentage
                                FileSize= fileSizeMB
                                Speed = float speed
                                FileRemaining=float((fileSize- transferred)/int64 1000/int64 1000)
                                Status=TransferStatus.Copying
                                EndTime=DateTime.Now}

        let outputStatsFileTrans  =Action<TransferProgress> (fun progress->
            if stopWatch.ElapsedMilliseconds>int64 500 then 
               setData (float progress.Percentage) progress.BytesTransferred
               )

        let ftpProgress:Progress<FtpProgress>  =new Progress<FtpProgress>(fun prog ->
            if stopWatch.ElapsedMilliseconds>int64 500 then 
                setData prog.Progress prog.TransferredBytes
            )
            
        if isFtp then (FtpProg ftpProgress)
        else (FileProg outputStatsFileTrans)