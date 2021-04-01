namespace TransferClient
open System
open System.Diagnostics;
open System.Threading
open System.IO
open System.Threading.Tasks
open System.IO.Pipelines
open FSharp.Control.Tasks
open System.Buffers
open FluentFTP
module Testing=
    let a=1
(*
    let Fmovertest()=
        async{
            let source2="./testSource2/Files2.zip" 
            let dest2= "H:/testDest2/Files2.zip"
            let source="./testSource2/Files.zip" 
            let dest= "H:/testDest2/Files.zip"
            let Callback=(fun x-> printfn "progress: %A"x)
            let ct= new Threading.CancellationTokenSource()
            let! jobA=IO.FileMove.FCopy  source2 dest2 Callback ct.Token|>Async.StartChild
            let! jobB=IO.FileMove.FCopy  source dest Callback ct.Token |>Async.StartChild
            let! resA=jobA
            let! resB=jobB
            return(resA,resB)
        }
        |>Async.RunSynchronously
    let ffmpegTest()=
        let source="./testSource2/bunTest.mxf" 
        let dest= "H:/testDest2/"
        let Callback=(fun x-> printfn "progress: %A"x)
        let ct= new Threading.CancellationTokenSource()
        let ffmpegInfo= IO.Types.TranscodeData [".mp4";".mxf"] None None
        let progress (sourceDuration:TimeSpan) (args:FFmpeg.NET.Events.ConversionProgressEventArgs)=
            printfn "%A" args
            if args.Bitrate.HasValue then
                let KBRate= double(args.Bitrate.Value/8.0)
                let MBrate=KBRate/1000.0
                let expectedSize= (sourceDuration.TotalSeconds*MBrate)
                //this means MB/s*speed multiplyer(frames per secondof video/number of frames being processed each second)
                let speed= if args.Fps.HasValue then (MBrate *(args.Fps.Value/24.0)) else 0.0

                
                let remaining= MBrate * float (sourceDuration- args.ProcessedDuration).TotalSeconds
                printfn "expected size %f "expectedSize
                printfn " speed %f "speed
                printfn "remaining %f "remaining
            ()
        //let moveJob=IO.VideoMover.Transcode ffmpegInfo None progress source dest ct.Token
        let time= Diagnostics.Stopwatch()
        time.Start()
        //Async.RunSynchronously moveJob
        time.Stop()
        printfn "time: %s" (time.Elapsed.ToString())
    
(*     let ReadError (reader:ProcessStreamReader  )(token:CancellationToken) =
      async{  while not token.IsCancellationRequested do
            Console.WriteLine(reader.ReadLine)
      }
     *)
    let outputPipe()=
        //ideas from here vv
//https://gist.github.com/bobend/ae229860d4f69c563c3555e3ccfc190d


        let output = "./testDest/test.mp4"
        let args = "-i ./testSource2/bunTest.mxf -c:v h264 -crf 20 -pix_fmt + -movflags frag_keyframe+empty_moov -g 52 -preset faster -flags +ildct+ilme -f mp4 -y pipe:1"
      (*   let mutable proces = new Process()
        using ( Command.Run("./ffmpeg.exe",sprintf" %s"args))
            (fun cmd->
                use outputStream = File.OpenWrite(output)
                let outputTask = cmd.StandardOutput.PipeToAsync(outputStream)
                let cs = new CancellationTokenSource()
                let logTask = ReadError cmd.StandardError cs.Token
                outputTask.Wait()
                cs.Cancel();
                let task2=Async.StartChild logTask
                Async.RunSynchronously task2|>ignore
            ) *)
        let mutable info = ProcessStartInfo("./ffmpeg.exe",args)
        info.RedirectStandardOutput<-true
        let mutable proc=new Process()
        proc.StartInfo<-info
        proc.Start()
        //proc.BeginOutputReadLine()
        let array_length = int (Math.Pow(2.0, 19.0))
        use fswrite = new FileStream (output, FileMode.Create, FileAccess.Write, FileShare.None, array_length )

        use bwWrite=new BinaryWriter(fswrite)
        let dataArray: array<byte> = Array.zeroCreate (array_length)

        let readBytes (bwwrite: BinaryWriter) =
            //number of bytes actaully read into the buffer, if all has been read it will be 0
            let read = proc.StandardOutput.BaseStream.Read(dataArray, 0, array_length)
            if 0 = read then
                //Finish reading
                false
            else
                fswrite.Write(dataArray, 0, read)
                //Continue reading
                true

        try
            let mutable keepReading = true

            while keepReading do
                    keepReading <- readBytes  bwWrite
        with _ -> ()

        ()   
        
        
    let pipeStream source dest= 
        
        let fillPipe (source:Stream) (writer:PipeWriter)=
            task{
                let minimumBufferSize = int (Math.Pow(2.0,10.0))
                let mutable reading=true
                while reading do
                    let memory =  (writer.GetMemory(minimumBufferSize))
                    let! bytesRead = source.ReadAsync( memory)
                    if bytesRead = 0 then
                        reading<-false
                    // Tell the PipeWriter how much was read from the Socket.
                    writer.Advance(bytesRead);
                    let! result = writer.FlushAsync()
                   
                    
                    if result.IsCompleted then
                        reading<-false
                do! writer.CompleteAsync()
            }
        
        
        let readPipe (output:Stream) (reader:PipeReader)=
            task{
                let mutable reading=true
                while reading do
                    let! result =  reader.ReadAsync();
                    let mutable buffer = result.Buffer;
                    let mutable line =System.Buffers.ReadOnlySequence<byte>()
                    let tryReadLine ()=
                        // Look for a EOL in the buffer.
                        let position = buffer.PositionOf((byte)'\n');
                    
                        if  not(position.HasValue) then
                            line<- Buffers.ReadOnlySequence<byte>() 
                            false 
                        else
                            // Skip the line + the \n.
                            line <- buffer.Slice(0, position.Value)
                            buffer <- buffer.Slice(buffer.GetPosition(1L, position.Value))
                            true

                    while tryReadLine() do
                        // Process the line.
                        do! output.WriteAsync(line.ToArray(),0,(int)line.Length)

                    // Tell the PipeReader how much of the buffer has been consumed.
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    // Stop reading if there's no more data coming.
                    if result.IsCompleted then
                        reading<-false
                do! reader.CompleteAsync();
            }
        

        let pipe = new Pipe();
        task{
        let writing = fillPipe source pipe.Writer
        let reading = readPipe dest (pipe.Reader)
        do!writing
        do!reading
        }

        
    let simplewriter (source:Stream) (dest:Stream)=
        task{
        let array_length = int (Math.Pow(2.0, 19.0))
        let dataArray: array<byte> = Array.zeroCreate (array_length)
        //this needs to be used if the ftp streamash been set to binary
        //use bwWrite=new BinaryWriter(dest)
        let mutable keepReading = true
        while keepReading do
            let! read = source.AsyncRead(dataArray, 0, array_length)
            if 0 = read then
                //Finish reading
                keepReading<- false
            else
               // bwWrite.Write(dataArray, 0, read)
                do!dest.AsyncWrite(dataArray, 0, read)
                //Continue reading
                keepReading <- true
        }

    let ffmpegstream args dest destFile (writer:Stream->Stream->Task<unit>)=
        
        let ftp = new FluentFTP.FtpClient("***REMOVED***","***REMOVED***","***REMOVED***")
        let mutable info = ProcessStartInfo("./ffmpeg.exe",args)
        info.RedirectStandardOutput<-true
        let mutable proc=new Process()
        proc.StartInfo<-info
        proc.Start()
        let ftpWriter= ftp.OpenWrite(dest+destFile,FluentFTP.FtpDataType.ASCII,false)
        (writer proc.StandardOutput.BaseStream ftpWriter).Wait()
        ftpWriter.Close() 
        let resply=ftp.GetReply() 
        printfn "reply= %A" resply.Message
        printfn "worked?= %A" resply.Success
        //ftp.UploadAsync(proc.StandardOutput.BaseStream,dest+"/ftpTest.mp4",FluentFTP.FtpRemoteExists.Overwrite)|>Async.AwaitTask 
    let ffmpegstream2 args dest destFile=
        let ftp = new FluentFTP.FtpClient("***REMOVED***","***REMOVED***","***REMOVED***")
        let mutable info = ProcessStartInfo("./ffmpeg.exe",args)
        info.RedirectStandardOutput<-true
        let mutable proc=new Process()
        proc.StartInfo<-info
        proc.Start()
        let ftpWriter= ftp.UploadAsync(proc.StandardOutput.BaseStream,dest+destFile,FluentFTP.FtpRemoteExists.Overwrite) 
        ftpWriter.Wait()
        //ftp.UploadAsync(proc.StandardOutput.BaseStream,dest+"/ftpTest.mp4",FluentFTP.FtpRemoteExists.Overwrite)|>Async.AwaitTask 
    let ffmpegstreamLibrary args dest destFile (writer:Stream->Stream->Task<unit>)=
        let ftp = new FluentFTP.FtpClient("***REMOVED***","***REMOVED***","***REMOVED***")

        ftp.Connect()
        (* let mutable info = ProcessStartInfo("./ffmpeg.exe",args)
        info.RedirectStandardOutput<-true
        let mutable proc=new Process()
        proc.StartInfo<-info
        proc.Start() *)
        let eng =new FFmpeg.NET.Engine("./ffmpeg.exe")
        let datahandler (arg:FFmpeg.NET.Events.ConversionDataEventArgs)=
            printfn " %s "arg.Data
            ()
        let Handler (arg)=
            printfn " %A "arg
            ()   
        let errorHandler (arg:FFmpeg.NET.Events.ConversionErrorEventArgs)=
            printfn "{FFMPEG ERROR:} %A "arg.Exception
            () 
       // eng.Data.Add datahandler
        eng.Progress.Add Handler
        eng.Error.Add errorHandler
(* 
        let (finishTask,ffmpegProcess)=eng.ExecuteStream( args,CancellationToken.None).ToTuple()
        //finishTask.Start();
        //ffmpegProcess.Start();

        printfn "can acess stdout %b" ffmpegProcess.StartInfo.RedirectStandardOutput
        try
            ftp.DeleteFile(dest+destFile)
        with|_->()
       
        
        finishTask.Start()
        let ftpWriter= ftp.OpenWrite(dest+destFile,FluentFTP.FtpDataType.ASCII,false)
        try
            (writer ffmpegProcess.StandardOutput.BaseStream ftpWriter).Wait()
        with
        |ex-> Logging.errorf "Something went wrong with the ffmpeg process make sure your ags a correct: %A" ex *)

            
        
        printfn"closing connection"
        
        //ftpWriter.Close() 
        let resply=ftp.GetReply() 
        printfn "reply= %A" resply.Message
        printfn "worked?= %A" resply.Success
        
        //ftp.UploadAsync(proc.StandardOutput.BaseStream,dest+"/ftpTest.mp4",FluentFTP.FtpRemoteExists.Overwrite)|>Async.AwaitTask 
   
    let simpleBinary (source:Stream) (dest:Stream)=
        task{
        let array_length = int (Math.Pow(2.0, 19.0))
        let dataArray: array<byte> = Array.zeroCreate (array_length)
        //this needs to be used if the ftp streamash been set to binary
        use bwWrite=new BinaryWriter(dest)
        let mutable keepReading = true
        while keepReading do
            let! read = source.AsyncRead(dataArray, 0, array_length)
            if 0 = read then
                //Finish reading
                keepReading<- false
            else
                bwWrite.Write(dataArray, 0, read)
                do!dest.AsyncWrite(dataArray, 0, read)
                //Continue reading
                keepReading <- true
        }
   (*  let systemwatcher  outputArgs dest destFile sourceFile=
        printfn "found file "
        let inputArgs= " -c:v mpeg2video -f mxf -i pipe:0"
        let ftp = new FluentFTP.FtpClient("***REMOVED***","***REMOVED***","***REMOVED***")
        let mutable info = ProcessStartInfo("./ffmpeg.exe", inputArgs+" "+outputArgs)
        info.RedirectStandardOutput<-true
        info.RedirectStandardInput<-true
        let mutable proc=new Process()
        proc.StartInfo<-info
        proc.Start()
        let ffmpegIn = proc.StandardInput.BaseStream;
        let writeFFmpeg=
            task{
                use readStream= new FileStream(sourceFile,FileMode.Open)
                do! simpleBinary readStream ffmpegIn
            }
        let readFFmpeg=
            task{
                
                
                do! simpleBinary proc.StandardOutput.BaseStream ftpWriter
                ftpWriter.Close() 
                let resply=ftp.GetReply() 
                printfn "reply= %A" resply.Message
                printfn "worked?= %A" resply.Success
            }    

        // After you are done
        ffmpegIn.Flush();
        ffmpegIn.Close();
        
        
        ()

    let watch()=
        let conf=File.ReadLines("config.txt")|>Seq.toList;

        let dest="***REMOVED***Transfers/SSC to BUN/testing/"
        let writer= 
            match conf.[2] with
            |"0"->simplewriter
            |"1" ->pipeStream
        let args=(conf.[0]+" pipe:1")
       
        let mutable foundFiles = [||]
        async{
        while true do 
            let newFiles=Watcher.checkForNewFiles2 foundFiles "./watchSource/"
            if newFiles.Length>0 then
                foundFiles<- Array.concat [newFiles;foundFiles]
                systemwatcher args dest conf.[1] newFiles.[0]
            do! Async.Sleep(100)
        }|>Async.RunSynchronously
        
 *)
        
  (*   let ffmpegCore args sourceFile dest destFile (writer:Stream->Stream->Task<unit>)=
            
        let ftp = new FluentFTP.FtpClient("***REMOVED***","***REMOVED***","***REMOVED***")
        let readStream= new FileStream(sourceFile,FileMode.Open)
        let writeStream= new FileStream("./testDest"+destFile,FileMode.Create)
        let ftpWriter= ftp.OpenWrite(dest+destFile,FluentFTP.FtpDataType.ASCII,false)
        let ffmpeg= 
            FFMpegArguments
                .FromPipe(new Pipes.StreamPipeSource(readStream))               
                .WithCustomArgument(args)
                .OutputToPipe(new Pipes.StreamPipeSink(ftpWriter))
                .ProcessAsynchronously()
                
        ffmpeg.Wait()
        ftpWriter.Close() 
        let resply=ftp.GetReply() 
        printfn "reply= %A" resply.Message
        printfn "worked?= %A" resply.Success *)
        //ftp.UploadAsync(proc.StandardOutput.BaseStream,dest+"/ftpTest.mp4",FluentFTP.FtpRemoteExists.Overwrite)|>Async.AwaitTask 
  (*   let streamTest()=
        let conf=File.ReadLines("config.txt")|>Seq.toList;

        let dest="***REMOVED***Transfers/SSC to BUN/testing/"
        let writer= 
            match conf.[2] with
            |"0"->simplewriter
            |"1" ->pipeStream
        if conf.[4] ="core" then 
            ffmpegCore (conf.[0]) "./watchSource/bunTest.mxf" dest conf.[1]  writer
        else if conf.[2] ="2" then 
            ffmpegstream2 (conf.[0]+" pipe:1 ") dest conf.[1]
        else
            ffmpegstreamLibrary (conf.[0]+" pipe:1 ") dest conf.[1] writer   
     *)
    let test number=
(*         printfn"testing %i"number
        let timer= Diagnostics.Stopwatch()
        timer.Start()
        streamTest()
        timer.Stop()
        printfn "took %f "timer.Elapsed.TotalSeconds

         *)
        let host ="***REMOVED***"
        let user= "quantel"
        let password="***REMOVED***"
        let client=new FtpClient(host, user ,password)
        client.Connect()
        client.DownloadFileAsync("./vdcp1Dest/testFile.mxf", "***REMOVED***RKY UPDATE 12 21 08.mxf")
        let host ="***REMOVED***"
        let user= "quantel"
        let password="***REMOVED***"
        let client2=new FtpClient(host, user ,password)
        client2.Connect()
        client2.DownloadFile("./vdcp1Dest/testFile2.mxf", "***REMOVED***RKY HEADLINES 21 08.mxf")
        

        //let job=FileTransferManager.CopyWithProgressAsync ("./testSource2/Files.zip", "H:/testDest2/Files.zip", Callback,false )
       // Async.RunSynchronously (Async.AwaitTask job)
       (*  let inPath= "./testSource/BUNPREMIER.mxf"
        let fileName=IO.Path.GetFileName inPath
        let outPath="quantel:***REMOVED***@***REMOVED***/***REMOVED***Transfers/SSC to BUN/"+fileName
        Transcode inPath true outPath  *)*)
     