namespace TransferClient.IO
open System
open FFmpeg.NET
open Types
open TransferClient
open FSharp.Control.Tasks.V2
open FluentFTP
open System.Diagnostics
open System.Threading
module VideoMover=
    /// This will take an ffmpeg stdout streama nd pipe it down an ftpwriting stream... not totally sure about this one working tbh
    let private ftpFFmpeg ftpData outpath (ffmpegProc:Process) =
        task{
            use ftpClient=new FtpClient(ftpData.Host,ftpData.User,ftpData.Password)
            ftpClient.Connect()
            let ftpWriter= 
                try
                //This must be ascii mode becuase that is what ffmpeg outputs
                Some( ftpClient.OpenWrite(outpath,FtpDataType.ASCII,false))
                with|ex->
                    Logging.errorf "{Fffmpeg} Exception in ftp opening for ffmpeg %A"ex
                    None
            if ftpWriter.IsNone then ()
            else
                try
                //this must not use a binary writer because ffmpeg outputs ascii data
                    do! StreamPiping.simplewriter ffmpegProc.StandardOutput.BaseStream ftpWriter.Value
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
    let defaultFtpArgs=" -c:v h264 -crf 18 -pix_fmt + -movflags frag_keyframe+empty_moov -g 52 -preset veryfast -flags +ildct+ilme -f mp4 "
    let private checkIfFileExists()=
        //TODO:Need to make this far more linux friendly. Ill ada  config optin in the yaml or something.
        match IO.File.Exists "./ffmpeg.exe" with
        |false-> 
            Logging.errorf "{FFmpeg} Could not find 'ffmpeg.exe' in root dir"
            Some TransferResult.Failed
        |true->
            None

    let private setupTranscode (filePath:string) progressHandler =
        async{
            let mutable ffmpegLog =""
            let fileName= IO.Path.GetFileName( filePath)  
            let mpeg = Engine("./ffmpeg.exe")
            //This is all error handling
            let mutable transferError=ref false
            let errorHandler (errorArgs:Events.ConversionErrorEventArgs)=
                transferError:=true
                try
                    Logging.errorf "{FFmpeg} transcode transfer for source: %s failed with error %A \n FFmpegLog= %s" fileName errorArgs.Exception ffmpegLog 
                with|_->  Logging.errorf "{FFmpeg} transcode transfer for source: %s failed with no error message "fileName
            mpeg.Error.Add errorHandler
            mpeg.Data.Add (fun arg->(ffmpegLog<-ffmpegLog+ arg.Data+"\n" ))
            
            
            // We have to get the source files duration becuase the totalDuration given as part of the progressargs object doesnt work
            let mediaFile=MediaFile(filePath)
            let! metaData= Async.AwaitTask( mpeg.GetMetaDataAsync(mediaFile))
            
            mpeg.Progress.Add (progressHandler metaData.Duration )
            return (transferError,mpeg)
        }
    type TranscodeType=
        |FTP of FTPData
        |File
        |CustomOutput of string
        //TODO: Need to figure out how to get the reciever ip into this func
    let prepArgs ffmpegInfo (transcodeType:TranscodeType) destFilePath (filePath:string)=
        let usedFFmpegArgs=
            match ffmpegInfo.FfmpegArgs with
            |Some x->x
            |None->
                Logging.errorf "ffmpeg args empty, cannot run transcode"
                failwith "No ffmpeg args. cannot continue"
        let outPath=  
            match ffmpegInfo.OutputFileExtension with 
            |Some x->IO.Path.ChangeExtension(destFilePath,x) 
            |None->IO.Path.ChangeExtension(destFilePath,"mp4")    
               
        let outArg=
                match transcodeType with
                |FTP inf-> sprintf " -y ftp://%s:%s@%s/%s"  inf.User inf.Password inf.Host outPath
                |File->" \""+outPath+"\""
                |CustomOutput outArgs->outArgs
                
        let args= sprintf "-i \"%s\" %s %s" filePath usedFFmpegArgs outArg    
        args

    let runTranscode (transferError:ref<bool>) args (mpeg:Engine) ct=
        async{
            Logging.infof "{FFmpeg} Calling with args: %s" args
            let task=mpeg.ExecuteAsync( args,ct)
            let! fin= Async.AwaitTask task
                       
            if !transferError then return  TransferResult.Failed
            else if ct.IsCancellationRequested then return TransferResult.Cancelled
            else return TransferResult.Success
        }
    ///Starts a send to a recieving ffmpeg instance.
    let sendToReceiver (ffmpegInfo:TranscodeData) (receiverFuncs:ReceiverFuncs) progressHandler  (filePath:string) (destFilePath:string) (ct:CancellationToken) =
        async{ 
            match checkIfFileExists() with
            |Some(err)->return err
            |None->
                try
                    //---check if the receiver is available and get an ip---
                    let recv=ffmpegInfo.ReceiverData.Value
                    let ip=receiverFuncs.GetReceiverIP recv.ReceivingClientName|>Async.RunSynchronously
                       (*  match receiverFuncs.GetReceiverIP recv.ReceivingClientName|>Async.RunSynchronously with
                        |Ok(x)-> 
                            Logging.infof "{VdieoMoveer} Got ip %s for receiver" x
                            x
                        |Error(x)->failwithf "{VideoMover} Could not get ip of reciver, job terminated. Reason:%s" x *)
                        
                    let outArgs= sprintf "-y %s://%s:%i?%s" recv.Protocoll ip recv.Port recv.ProtocolArgs
                    let! (transcodeError,mpeg)= setupTranscode filePath progressHandler;
                    let args=prepArgs ffmpegInfo (CustomOutput outArgs) destFilePath filePath;
                    //vv--start the ffmpeg instance listening--vv 
                    let res=runTranscode transcodeError args mpeg ct
                    let reciverStarted=
                        match (receiverFuncs.StartTranscodeReciever recv.ReceivingClientName recv.ReceivingFFmpegArgs)|>Async.RunSynchronously with
                        |true->()
                        |false->
                            //we want to wait for the tcp connection timeout
                            mpeg.Complete|>Async.AwaitEvent|>Async.RunSynchronously|>ignore
                            failwithf "{VideoMover} Could not get ip of reciver, job terminated. Reason:" 
                    //--send a messsage that starts the client connecting---
                    //--wait for completion---
                    return! res
                with|err->
                    Logging.errorf "Transcode job failed, reason:%A" err
                    return TransferResult.Failed
                    
                    
            }
    let startReceiving args=
        async{
            let mutable ffmpegLog =""
          
            let mpeg = Engine("./ffmpeg.exe")
            //This is all error handling
            let mutable transferError=ref false
            let errorHandler (errorArgs:Events.ConversionErrorEventArgs)=
                transferError:=true
                try
                    Logging.errorf "{FFmpeg} transcode Receiving failed with error %A \n FFmpegLog= %s"errorArgs.Exception ffmpegLog 
                with|_->  Logging.errorf "{FFmpeg} Transcode Receiving failed with no error message "
            mpeg.Error.Add errorHandler
            mpeg.Data.Add (fun arg->(ffmpegLog<-ffmpegLog+ arg.Data+"\n" ))
            
            
            let ct=new CancellationTokenSource()
            let! res= runTranscode transferError args mpeg ct.Token
            return res
        }
    ///outPath should either be a straight filepath or an FTp path 
    let Transcode ffmpegInfo (ftpInfo:FTPData option) progressHandler (filePath:string)  (destDir:string) (ct:CancellationToken)=

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
                    Logging.warnf "ffmpeg args empty, using defaalt args"
                    match ftpInfo with
                    |Some _-> defaultFtpArgs
                    |None-> defaultArgs

                //This is the old way that used a pipe allowed me to use acustom ftp implmenttaion
           (*  let outArg=
                match ftpInfo with
                |Some x-> " -y  pipe:1" 
                |None->" \""+outPath+"\"" *)

            (* try 
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
                    transferError<-true          *)   

            //This is the new way. it just uses ffmpeg's built in ftp
            let outArg=
                match ftpInfo with
                |Some inf-> sprintf " -y ftp://%s:%s@%s/%s"  inf.User inf.Password inf.Host outPath
                |None->" \""+outPath+"\""
                
            let args= sprintf "-i \"%s\" %s %s" filePath usedFFmpegArgs outArg

            Logging.infof "{FFmpeg} Calling with args: %s" args
            let task=mpeg.ExecuteAsync( args,ct)
            let! fin= Async.AwaitTask task
                       
            if transferError then return  TransferResult.Failed
            else if ct.IsCancellationRequested then return TransferResult.Cancelled
            else return TransferResult.Success
        }