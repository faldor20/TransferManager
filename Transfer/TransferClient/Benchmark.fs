namespace TransferClient
open TransferClient
open TransferClient.IO
open FluentFTP
open System.IO
open System

module Benchmark=
    let GetClients()=
        let host ="***REMOVED***"
        let user= "***REMOVED***admin-***REMOVED***"
        let password="***REMOVED***"
        let bfs=new FtpClient(host, user ,password)
        bfs.Connect()
        printfn "connected to bfs: %b"bfs.IsConnected
        let bun = new FluentFTP.FtpClient("***REMOVED***","FTPuser","Password")
        bun.Connect()
        printfn "connected to bun: %b"bun.IsConnected
        (bun,bfs)
    let timer=System.Diagnostics.Stopwatch()
    timer.Start()
    let ftpProgress name=
        new Progress<FtpProgress>(fun prog ->
            if(timer.ElapsedMilliseconds=int64 1000)then
                printfn "proress %s: %f" name prog.Progress
                timer.Reset()
            ()
            )
    /// A metho to copy from one ftp server to another not as efficient as fxp beuase it must be ransfered through the server running the transferclient but still very fast
    let ftpStreams (client1:FtpClient) (client2:FtpClient) sourcePath destPath=
        async {
            let size=client1.GetFileSize(sourcePath)
            printfn "doing ftp to ftp transfer from %s to %s" sourcePath destPath
            let intermediateStream = new MemoryStream( int size)
            let outStream=client2.OpenWrite(destPath)
            let! a = Async.AwaitTask<|client1.DownloadAsync(outStream,sourcePath,int64 0,ftpProgress "down")
            outStream.Close()
            let! reply=Async.AwaitTask <|client2.GetReplyAsync(new Threading.CancellationToken())
            printfn "replyworked? %b message= %s " reply.Success reply.Message
            return ()
        }
        ///Doesn't work with growing files
    let ftpreadStream (client1:FtpClient) (client2:FtpClient) sourcePath destPath=
        async {
                   let size=client1.GetFileSize(sourcePath)
                   printfn "doing ftp to file transfer from %s to %s" sourcePath  destPath
                   let outStream=client1.OpenRead(sourcePath)
                   use file = new FileStream(destPath,FileMode.Create,FileAccess.Write,FileShare.Read,(1024*512))

                   outStream.CopyTo(file)
                   file.Flush()
                   file.Close()
                   outStream.Close()

                   let! reply=Async.AwaitTask <|client2.GetReplyAsync(new Threading.CancellationToken())
                   printfn "replyworked? %b message= %s " reply.Success reply.Message
                   return ()
               }
               ///Doesn't work with growing files
    let ftpPipeStream (client1:FtpClient) (client2:FtpClient) sourcePath destPath=
           async {
                   
                      printfn "doing ftp to file transfer from %s to %s" sourcePath  destPath
                      let readStream=client1.OpenRead(sourcePath)
                      use file = new FileStream(destPath,FileMode.Create,FileAccess.Write,FileShare.Read,(1024*512))
                      let! task=Async.AwaitTask<| IO.StreamPiping.pipeStream readStream file
                      file.Flush()
                      file.Close()
                      readStream.Close()

                      let! reply=Async.AwaitTask <|client2.GetReplyAsync(new Threading.CancellationToken())
                      printfn "replyworked? %b message= %s " reply.Success reply.Message
                      return ()
                  }

    let timeOut name func=
        let stopWatch = System.Diagnostics.Stopwatch.StartNew()
        Async.RunSynchronously func|>ignore
        stopWatch.Stop()
        printfn "time %s  %f" name stopWatch.Elapsed.TotalMilliseconds

    let FTPtoFTP sourceFile destination fileName=
        let (bun,bfs)=GetClients()
        
        (ftpStreams bun bfs sourceFile (destination+fileName))
        |>timeOut "FtptoFtp"

    let FTPStreamtoFile sourceFile fileName=
           let (bun,bfs)=GetClients()
           let sourceFile2="***REMOVED***Transfers/Testing/stream/test.mxf"
           let destdir= "./testDest/"
           let fileName="test.mxf"
           (ftpPipeStream bun bfs sourceFile2 (destdir+fileName))
           |>timeOut "FTPStreamToFile"

    let benchmark()=
        let sourceFile= "***REMOVED***Transfers/Testing/TOSSC/WBY  HEADLINES  26-08.mxf"
        let destdir= "./"
        let fileName="test.mxf"
        FTPStreamtoFile sourceFile fileName

    

