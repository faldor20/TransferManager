// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open Mover
open System
open System.IO
open System.IO.Pipes
open Mover.Types
open Serilog
open LoggingFsharp
open System.Diagnostics
open System.Net
open System.Net.Sockets
let setupLogging()=
    Serilog.Log.Logger<-
        (new LoggerConfiguration())
            .MinimumLevel.Debug()
            .WriteTo.Console(theme=Sinks.SystemConsole.Themes.SystemConsoleTheme.Literate)
            .CreateLogger();
    ()
let listen() =
    async{
        let listener=Sockets.TcpListener.Create(5678)
        listener.Start()
        listener.Server.SendBufferSize<-9000111
        printfn "waiting for incoming connection"
        let client=listener.AcceptTcpClient();
        printfn "got connection"
        let clientStream=client.GetStream()
        return clientStream
    }

let setupPipe() =
    async{
        let pipeServer=new NamedPipeServerStream "ffmpegOut"
        Lginfof "waiting for pipe connection"
        pipeServer.WaitForConnection()
        Lginfof " got connection starting pipe"
        return pipeServer
    }


let transcodeData=
    let args=Some "-c:v h264  -crf 10 -pix_fmt + -preset ultrafast -f mpegts"
    let outputExt= None
    let receiverData=None
    let transcodeExtensions=[]
    TranscodeData args outputExt receiverData transcodeExtensions
let run=
    VideoMover.transcodeToCustom "\\\\.\\pipe\\ffmpegOut"

let doTranscode ()=
    async{
        let handler:ProgressHandlers.TranscodeProgress=(fun time progress-> printfn "Progress:%A" progress )
        Mover.VideoMover.ffmpegPath<-Some "C:\\Users\\***REMOVED***\\scoop\\shims\\ffmpeg.exe"
        let destpath= "./oops.mp4"
        let sourcePath= "../TransferClient/testSource/BUN-Prem-Test.mxf"
        let ct=new Threading.CancellationTokenSource()
        //Create the pipe and tcp socket.
        //TODO: find out if the socket and pipe buffer can be icreased to improve the performnace
        let! waitStream=listen()|>Async.StartChild
        let! waitPipe= setupPipe()|>Async.StartChild
        //message client to tell it to connect to the newly created socket
        //----message the client code goes here---
        //actaully start the ffmpeg instance
        let! task=run transcodeData handler sourcePath destpath ct.Token|>Async.StartChild
        //Wait for the cleint to connect to the tcp socket and for the local ffmpeg to connect to the pipe
        let! tcpStream=waitStream
        let! ffmpegStream=waitPipe
        //move the data from one stream to another
        (StreamPiping.pipeStream ffmpegStream tcpStream).Wait()
        return! task
    }
        
let runProgram()=
    try
    let result=doTranscode()|>Async.RunSynchronously
    Lginfo "result is :{@res}" result
    with|e->printfn "Transcode faile with exception: %A"e
  
    0

[<EntryPoint>]
let main argv =
    setupLogging()|>ignore
    let message =  "F#" // Call the function
    printfn "Hello world %s" message
    Lginfof "starting"
    runProgram()
    0 // return an integer exit code