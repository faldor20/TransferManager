module Mover.VideoMover
open System
open FFmpeg.NET
open Types
open LoggingFsharp
open FSharp.Control.Tasks.V2
open FluentFTP
open System.Diagnostics
open System.Threading
open ProgressHandlers
open System.Net
open System.Net.Sockets
open Mover.StreamPiping
open System.IO.Pipes
module Logging=LoggingFsharp
//TODO: This is actually cancer... you need to set this somewhere manually, there is no constructor, you just mutate it.
//It's so ugly but it works.
let mutable ffmpegPath:string option=None
let portOffset = ref 0;
/// This will take an ffmpeg stdout stream and pipe it down an ftpwriting stream... not totally sure about this one working tbh
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
    mpeg.Data.Add (fun x-> x.Data|>Lgverb "'FFmpeg' Data: {@data} ")
    mpeg.Error.Add errorHandler
    mpeg.Data.Add (fun arg->(ffmpegLog<-ffmpegLog+ arg.Data+"\n" ))
    Logging.Lgdebugf "'VideoMover' FFmpeg error logging setup."
    transferError

let private setupTranscode (filePath:string) (progressHandler:TranscodeProgress) =
    let mpeg=createMpegEngine()
    let transferError=startMpegErrorLogging mpeg
    // We have to get the source files duration becuase the totalDuration given as part of the progressargs object doesnt work
    mpeg.Progress.Add (progressHandler )
    (transferError,mpeg)
        

type  OutputType=
    |FTP of FTPData
    |File
    |CustomOutput of string

type InputType=
    |FTP of FTPData
    |File 
///Ensures that the path begins with '/'
let private ensureAbsPath (filePath:string)=
    match filePath.StartsWith('/') with
        | true-> filePath
        |false -> "/"+filePath
let private prepOutputArgs outputType outPath=    
    match outputType with
    |OutputType.FTP inf-> sprintf " -y \"ftp://%s:%s@%s%s\""  inf.User inf.Password inf.Host (outPath|>ensureAbsPath)
    |OutputType.File->" \""+outPath+"\""
    |CustomOutput outArgs->outArgs    
let private prepArgs (ffmpegArgs:string option) outputExtension (outputType:OutputType) (inputType:InputType) destFilePath (filePath:string)=
    
    let ffmpegArgs=
        ffmpegArgs |>Option.defaultWith((fun _->    
            Logging.Lgerrorf "ffmpeg args empty, cannot run transcode"
            failwith "No ffmpeg args. cannot continue"))
    
    let outPath=IO.Path.ChangeExtension(destFilePath,outputExtension) 
               
    let outArg=prepOutputArgs outputType outPath
                
    let args= 
        let inArgs=
            match inputType with
            |File -> sprintf "\"%s\"" filePath   
            |FTP inf-> 
                sprintf "\"ftp://%s:%s@%s%s\""  inf.User inf.Password inf.Host (filePath|>ensureAbsPath)
        sprintf "-i %s %s %s" inArgs ffmpegArgs outArg
        
    args

let private runTranscode (transferError:ref<bool>) args (mpeg:Engine) ct=
    async{
        Logging.Lginfo "'VideoMover' Calling ffmpeg with args: {@args}" args
        let task=mpeg.ExecuteAsync( args,ct)
        let! fin= Async.AwaitTask task
        // the assumption is taht if the task ended and cancellation was requested. the task musthave been cancelled                
        if ct.IsCancellationRequested then return TransferResult.Cancelled
        else if !transferError then return  TransferResult.Failed
        else return TransferResult.Success
    }

//----------------------------------------
//=================Main Funcs==============
//-----------------------------------------

let private listen port  =
    async{
        let listener=Sockets.TcpListener.Create(port)
        listener.Start()
        listener.Server.SendBufferSize<-9000111
        Lgdebug "'VideoMover' Waiting for incoming tcp connection on port:{port}" port
        //TODO: i need to find out if i can actually dispose of this here. it may need to be kept untill the stream is disposed of
        //if so i should reutn the client and let me get the stream from that out of this scope
        use client=listener.AcceptTcpClient();
        Lgdebug "'VicdeoMover'Got tcp connection on port:{port}"port
        let clientStream=client.GetStream()
        return clientStream
    }

let private setupPipe (pipeName:string)  =
    async{
        let pipeServer=new NamedPipeServerStream(pipeName)

        Lgdebug "'VideoMover' Waiting for pipe:{name} connection" pipeName
        pipeServer.WaitForConnection()
        Lgdebug "'VideoMover' Got connection to pipe:{name}" pipeName
        return pipeServer
    }

let private customTCPSend customTCPData port startReceiver createTranscodeTask =
    async{
        //create a unique name for our pipe
        let id=Guid.NewGuid().ToString("N")
        let pipeName="ffmpg"+id
        
        let outArgs= "\\\\.\\pipe\\"+pipeName
        let transcodeTask= createTranscodeTask outArgs
        Lgdebugf "'VideoMover' Creating tcp server and Named pipe"
        let! waitStream=listen port|>Async.StartChild
        let! waitPipe= setupPipe pipeName|>Async.StartChild
        //message client to tell it to connect to the newly created socket
        startReceiver()
        //actaully start the ffmpeg instance
        let! res= transcodeTask|>Async.StartChild
        //Wait for the client to connect to the tcp socket and for the local ffmpeg to connect to the pipe
        Logging.Lgdebugf "'VideoMover' Waiting form tcp connection from cleint and pipe connection from local ffmpeg "
        use! tcpStream=waitStream
        use! ffmpegStream=waitPipe
        Logging.Lgdebugf "'VideoMover' Sending data from pipe to tcp stream"
        //move the data from one stream to another
        (StreamPiping.pipeStream ffmpegStream tcpStream).Wait()
        let! out=res
        return out
    }
let private ffmpegProtocolSend (data:ffmpegProtocol) port startReceiver createTranscodeTask=
    async{
        let outArgs= sprintf "-y %s://0.0.0.0:%i?%s" data.Protocoll port data.ProtocolArgs
        let transcodeTask=createTranscodeTask outArgs
        let! res= transcodeTask|>Async.StartChild
        startReceiver()
        return! res
         
    }
let private getFreePort startPort =
    let port=
        lock portOffset (fun()->
            let port=startPort+(!portOffset%200)
            portOffset:=(!portOffset+1)
            port
         ) 
    port
    
let private  makeReceiverArgs recv outPut port=
    let args=(recv.ReceivingFFmpegArgs+" "+outPut)
    if recv.DynamicPort then 
        let newargs=args.Replace("##port##",port.ToString())
        if newargs=args then 
            Lgerrorf "'VideoMover' you have selected dynamicport but have not included '##port##' in your receiver args. This means the port has not been dynamically set."
            raise (ArgumentException("DynamicPort is true and ReceiverArgs does not contain '##port##' to be replaced with the port number.  "))
        else newargs
    else args

let private handleTranscodeErrors (prefix:string) (filePath:string) f =
   async{
        try
               return! f()
        with
            | :? Tasks.TaskCanceledException as ex->
                Lgwarn3 "{@prefix} from {@path} was canceled {@ex}" prefix filePath ex
                return TransferResult.Cancelled
            |err->
                Lgerror3 "{@prefix} from {@path} failed {@ex}" prefix filePath err
                return TransferResult.Failed
    }            
let sendToReceiver inputType (outputType:OutputType) (receiverFuncs:ReceiverFuncs) (ffmpegInfo:TranscodeData) progressHandler  (filePath:string) (destFilePath:string) (ct:CancellationToken)=
    handleTranscodeErrors "'Video Mover' SendToReceiver transcode job " filePath (fun()->
            //---check if the receiver is available and get an ip---
            let recv=
                match ffmpegInfo.ReceiverData with
                |Some(x)->x
                |None->
                    Logging.Lgerrorf "'VideoMover' tried to run send to recceiver function but ffmpeg data's receiverdata was set to 'NOne'"
                    raise (new ArgumentException("see above^^"))
            //here we select which sneding method to use based on the union provided
            let runSend=
                match recv.SendMethod with
                |CustomTCP data->customTCPSend data
                |InbuiltCustom data->ffmpegProtocolSend data


            let port =
                if recv.DynamicPort then getFreePort recv.Port
                else recv.Port
                
            let outPath=IO.Path.ChangeExtension( destFilePath,ffmpegInfo.OutputFileExtension)
            let receiverOutput=prepOutputArgs outputType outPath
            let recvArgs= makeReceiverArgs recv receiverOutput port
               
                

            let startReceiver()=
                Lginfo "Video Mover' Sending message to receiver to start ffmpeg with these args: {@args}" recvArgs
                match (receiverFuncs.StartTranscodeReciever recv.ReceivingClientName recvArgs)|>Async.RunSynchronously with
                |true->()
                |false->
                    failwithf "'VideoMover' Could not get ip of reciver, job terminated. Reason:" 
                    
            let transcodeTask outArgs=
                let (transcodeError,mpeg)= setupTranscode filePath progressHandler;
                let args=prepArgs ffmpegInfo.FfmpegArgs "" (CustomOutput outArgs) inputType destFilePath filePath;
                //It is importnt to remeber the async job is not actually started here just created ready to be started elsewhere
                runTranscode transcodeError args mpeg ct
            
            let res= runSend port startReceiver transcodeTask
            res
    )
                

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

    
type sourceFilePath=  string
type destFilePath=string

let basicTranscode (outType:OutputType) inType ffmpegInfo (progressHandler:TranscodeProgress) (filePath:sourceFilePath)   (destDir:destFilePath) (ct:CancellationToken) =
    Lginfo3 "'Video Mover' Setting up basic transcode of InType: {@inType} outType: {@outType} from {@src}" inType outType filePath
    handleTranscodeErrors "'Video Mover' Transcode Job" filePath (fun()->
        let (transcodeError,mpeg)= setupTranscode filePath progressHandler;
        let args=prepArgs ffmpegInfo.FfmpegArgs "" outType inType destDir filePath;
        let res=runTranscode transcodeError args mpeg ct
        
        res
    )
(* 
/// Transcodes a file onto an ftp server
let transcodetoFTP destftpInfo =    
    basicTranscode (FTP destftpInfo) 
///Transcodes the given file and outputs to a filePath
let transcodeFile =
    basicTranscode File 
//Transcodes a file using the given args.
let transcodeToCustom args=
    basicTranscode (CustomOutput args)  *)
        