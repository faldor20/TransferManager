namespace TransferClient.IO
open System
open FFmpeg.NET
open Types
open TransferClient
open FSharp.Control.Tasks.V2
open FluentFTP
open System.Diagnostics

module VideoMover=
    let ftpFFmpeg ftpData outpath (ffmpegProc:Process) =
        task{
            use ftpClient=new FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
            ftpClient.Connect()
            let ftpWriter= 
                try
                Some( ftpClient.OpenWrite(outpath,FtpDataType.Binary,false))
                with|ex->
                    Logging.errorf "{Fffmpeg} Exception in ftp opening for ffmpeg %A"ex
                    None
            if ftpWriter.IsNone then ()
            else
                try
                    do! StreamPiping.simpleBinaryWriter ffmpegProc.StandardOutput.BaseStream ftpWriter.Value
                with|ex-> Logging.errorf "{Ffmpeg} Exception in stream copying from ffmpeg to ftp %A" ex
                
                ftpWriter.Value.Close() 
                let reply=ftpClient.GetReply() 
                if not reply.Success then 
                    Logging.errorf "{FFmpeg} ftp failed in some way. code %s \n ErrorMessage: %s " reply.Code reply.ErrorMessage
                Logging.infof "{FFmpeg} FTP reply= %A" reply.Message

        }

    let defaultArgs=" -c:v h264 -crf 18 -pix_fmt + -preset veryfast -flags +ildct+ilme"
    //we must include "-movflags faststart" in ftp becuase it makes the mp4 sreamable
    //-movflags frag_keyframe+empty_moov -g 52
    let defaultFtpArgs=" -c:v h264 -crf 18 -pix_fmt + -movflags frag_keyframe+empty_moov -g 52 -preset veryfast -flags +ildct+ilme -f h264 "


    /// outPath should either be a straight filepath or an FTp path 
    let Transcode ffmpegInfo (ftpInfo:FTPData option) progressHandler (filePath:string)  (destDir:string) ct=

        async{
        if not(IO.File.Exists "./ffmpeg.exe") then 
            Logging.errorf "{FFmpeg} Could not find 'ffmpeg.exe' in root dir"
            return TransferResult.Failed
        else
            let mutable FfmpegLog =""
            let fileName= IO.Path.GetFileName( filePath)  
            let mpeg = new Engine("./ffmpeg.exe")
            //This is all error handling
            let mutable transferError=false
            let errorHandler (errorArgs:Events.ConversionErrorEventArgs)=
                transferError<-true
                try
                    Logging.errorf "{FFmpeg} transcode transfer for source: %s failed with error %A \n FFmpegLog= %s" fileName errorArgs.Exception FfmpegLog 
                with|_->  Logging.errorf "{FFmpeg} transcode transfer for source: %s failed with no error message "fileName
            mpeg.Error.Add errorHandler
            mpeg.Data.Add (fun arg->(FfmpegLog<-FfmpegLog+ arg.Data+"\n" ))
            
            
            // We have to get the source files duration becuase the totalDuration given as part of the progressargs object doesnt work
            let mediaFile=MediaFile(filePath)
            let! metaData= Async.AwaitTask( mpeg.GetMetaDataAsync(mediaFile))
            
            mpeg.Progress.Add (progressHandler metaData.Duration )

            let outPath=  
                match ffmpegInfo.OutputFileExtension with 
                |Some x->IO.Path.ChangeExtension(destDir+fileName,x) 
                |None->IO.Path.ChangeExtension(destDir+fileName,"mp4")    
           

            // We woudn't normally be abe to use mp4 becuase i isn't streamable but with the right flags we can make it work
            
            let usedFFmpegArgs=
                match ffmpegInfo.FfmpegArgs with
                |Some x->x
                |None->
                    match ftpInfo with
                    |Some _-> defaultFtpArgs
                    |None-> defaultArgs
            let outArg=
                match ftpInfo with
                |Some x-> " -y  pipe:1" 
                |None->" \""+outPath+"\""
            let args="-i "+"\""+filePath+"\" "+usedFFmpegArgs+outArg 
            
            Logging.infof "{FFmpeg} Calling with args: %s" args
            try 
                //We use execute stream beuase it returns the process wheich can output from ffmpeg
                let (task,proc)=mpeg.ExecuteStream( args,ct).ToTuple()
                task.Start()
                match ftpInfo with
                |Some x-> do! (ftpFFmpeg x outPath proc |>Async.AwaitTask)
                |None ->()
                do! Async.AwaitTask task
            with
                |ex-> 
                    Logging.errorf "{FFmpeg} Transcode failed with error:%A \n FFmpegLog= %s" ex FfmpegLog
                    transferError<-true
                      
            if transferError then return  TransferResult.Failed
            else if ct.IsCancellationRequested then return TransferResult.Cancelled
            else return TransferResult.Success
        }