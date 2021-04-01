
module Mover.VideoMover
open System
open FFmpeg.NET
open Types
open LoggingFsharp
open FSharp.Control.Tasks.V2
open FluentFTP
open System.Diagnostics
open System.Threading
module Logging=LoggingFsharp

let mutable ffmpegPath:string option=None
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
                Logging.Lgerror "'Fffmpeg' Exception in ftp opening for ffmpeg {@excep}"ex
                    
                None
        if ftpWriter.IsNone then ()
        else
            try
            //this must not use a binary writer because ffmpeg outputs ascii data
                do! StreamPiping.simplewriter ffmpegProc.StandardOutput.BaseStream ftpWriter.Value
            with|ex-> Logging.Lgerror "'Ffmpeg' Exception in stream copying from ffmpeg to ftp {@excep}" ex
                
            ftpWriter.Value.Close() 
            let reply=ftpClient.GetReply() 
            if not reply.Success then 
                Logging.Lgerror2 "'FFmpeg' ftp failed in some way. code {@code} \n ErrorMessage:{@err} " reply.Code reply.ErrorMessage
            Logging.Lginfo "'FFmpeg' FTP reply={@reply}" reply.Message

    }

let defaultArgs=" -c:v h264 -crf 18 -pix_fmt + -preset veryfast -flags +ildct+ilme"
//we must include "-movflags faststart" in ftp becuase it makes the mp4 sreamable
//-movflags frag_keyframe+empty_moov -g 52
let defaultFtpArgs=" -c:v h264 -crf 18 -pix_fmt + -movflags frag_keyframe+empty_moov -g 52 -preset veryfast -flags +ildct+ilme -f mp4 "


let private createMpegEngine()=
    match ffmpegPath with
    |None->
            failwithf "'VideoMover' No ffmpegPath set please set it in the config file"
    |Some(ffmpegPath)->
        if not(IO.File.Exists ffmpegPath) then 
            Logging.Lgerror "'FFmpeg' Could not find 'ffmpeg' at path: {@path}" ffmpegPath
            failwith "see above^^"
        else
            Logging.Lgdebugf "'VideoMover' FFmpeg executable exists. Making engine."
            Engine(ffmpegPath)
        
    
let private startMpegErrorLogging (mpeg:Engine)=
    let mutable ffmpegLog =""
        
    //This is all error handling
    let mutable transferError=ref false
    let errorHandler (errorArgs:Events.ConversionErrorEventArgs)=
        transferError:=true
        try
            Logging.Lgerror2 "'FFmpeg' Transcode transfer failed with error {@err} \n FFmpegLog= {@log}"  errorArgs.Exception ffmpegLog 
        with|_->  Logging.Lgerrorf "'FFmpeg' transcode transfer failed with no error message "
    mpeg.Error.Add errorHandler
    mpeg.Data.Add (fun arg->(ffmpegLog<-ffmpegLog+ arg.Data+"\n" ))
    Logging.Lgdebugf "'VideoMover' FFmpeg error logging setup."
    transferError

let private setupTranscode (filePath:string) progressHandler =
    let mpeg=createMpegEngine()
    let transferError=startMpegErrorLogging mpeg
    // We have to get the source files duration becuase the totalDuration given as part of the progressargs object doesnt work
    let mediaFile=MediaFile(filePath)
    let metaData= Async.AwaitTask( mpeg.GetMetaDataAsync(mediaFile))|>Async.RunSynchronously
        
    mpeg.Progress.Add (progressHandler metaData.Duration )
    (transferError,mpeg)
        

type private TranscodeType=
    |FTP of FTPData
    |File
    |CustomOutput of string
    
let private prepArgs ffmpegArgs outputExtension (transcodeType:TranscodeType) destFilePath (filePath:string)=
    let usedFFmpegArgs=
        match ffmpegArgs with
        |Some x->x
        |None->
            Logging.Lgerrorf "ffmpeg args empty, cannot run transcode"
            failwith "No ffmpeg args. cannot continue"
    let outPath=  
        match outputExtension with 
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
        Logging.Lginfo "'FFmpeg' Calling with args: {@args}" args
        let task=mpeg.ExecuteAsync( args,ct)
        let! fin= Async.AwaitTask task
                       
        if !transferError then return  TransferResult.Failed
        else if ct.IsCancellationRequested then return TransferResult.Cancelled
        else return TransferResult.Success
    }
///Starts a send to a recieving ffmpeg instance.
let sendToReceiver  (receiverFuncs:ReceiverFuncs) (ffmpegInfo:TranscodeData) progressHandler  (filePath:string) (destFilePath:string) (ct:CancellationToken) =
    async{ 
        
        try
            //---check if the receiver is available and get an ip---
            let recv=
                match ffmpegInfo.ReceiverData with
                |Some(x)->x
                |None->
                    Logging.Lgerrorf "'VideoMover' tried to run send to recceiver function but ffmpeg data's receiverdata was set to 'NOne'"
                    raise (new ArgumentException("see above^^"))
             
                 
            let ip="0.0.0.0"
            let outArgs= sprintf "-y %s://%s:%i?%s" recv.Protocoll ip recv.Port recv.ProtocolArgs

            let (transcodeError,mpeg)= setupTranscode filePath progressHandler;
            let args=prepArgs ffmpegInfo.FfmpegArgs None (CustomOutput outArgs) destFilePath filePath;
            //vv--start the ffmpeg instance listening--vv 
            let res=runTranscode transcodeError args mpeg ct
            let outPath=
                IO.Path.ChangeExtension( destFilePath,"mxf")
            let recvArgs=
                recv.ReceivingFFmpegArgs+" \""+outPath+"\""
            Logging.Lgdebug "'VideoMover' Telling reciver to connect to us with args {@args} " recvArgs
            let receiverStarted=
                match (receiverFuncs.StartTranscodeReciever recv.ReceivingClientName recvArgs)|>Async.RunSynchronously with
                |true->()
                |false->
                    //we want to wait for the tcp connection timeout
                    mpeg.Complete|>Async.AwaitEvent|>Async.RunSynchronously|>ignore
                    failwithf "'VideoMover' Could not get ip of reciver, job terminated. Reason:" 
            //--send a messsage that starts the client connecting---
            //--wait for completion---
            return! res
        with|err->
            Logging.Lgerrorf "Transcode job failed, reason:%A" err
            return TransferResult.Failed
                
                
        }
///This is a simplified transcode function designed to be called to start a reciver 
let startReceiving args=
    async{
        //TODO: i could make this into an event handler that is started on program intialisation. that would allow me to read the ffmpeg path from the config file
        Logging.Lginfof "'VideoMover' Receiving ffmpeg stream with args %s "args
        let mpeg=createMpegEngine()
        let transferError=startMpegErrorLogging  mpeg
        let ct=new CancellationTokenSource()
        let! res= runTranscode transferError args mpeg ct.Token
        return res
    }

    
let private basicTranscode outType ffmpegInfo progressHandler (filePath:string)   (destDir:string) (ct:CancellationToken) =
    async{ 
        
        try
            let (transcodeError,mpeg)= setupTranscode filePath progressHandler;
            let args=prepArgs ffmpegInfo.FfmpegArgs None outType  destDir filePath;
            let res=runTranscode transcodeError args mpeg ct
            return! res
        with|err->
            Logging.Lgerrorf "Transcode job failed, reason:%A" err
            return TransferResult.Failed
                
    }
/// Transcodes a file onto an ftp server
let transcodetoFTP destftpInfo=    
    basicTranscode (FTP destftpInfo)
///Transcodes the given file and outputs to a filePath
let transcodeFile=
    basicTranscode File
         
        