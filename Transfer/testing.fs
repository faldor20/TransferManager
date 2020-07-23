namespace Transfer
open System
open System.Diagnostics
open System.Threading
open FSharp.Control.Reactive
open FSharp.Control
open FluentFTP
open FFmpeg.NET
//open System.Reactive
open ProgressHandlers
module Testing=
    let progressHandler (arg:Events.ConversionProgressEventArgs)=
        printfn "%A" arg
        ()
    let dataHandler (arg:Events.ConversionDataEventArgs)=
        printfn "%A" arg.Data
        ()

    let Transcode filePath isFtp (outPath:string)=
        let mpeg = new Engine("./ffmpeg.exe")
        // we must use an mkv here becuase it is a format that supports stream writing. that is writing without reading from the file
        let correctPath=IO.Path.ChangeExtension(outPath,"mp4") 
        let out=
            match isFtp with
            |true-> "ftp://"+correctPath
            |false-> correctPath
        let args="-i "+filePath+" -c:v h264 -crf 20 -pix_fmt + -preset faster -flags +ildct+ilme -movflags frag_keyframe+empty_moov "+"\""+out+"\"" 
        printfn "call ffmpeg with args: %s" args
        let res=mpeg.ExecuteAsync( args)
        mpeg.Progress.Add progressHandler
        mpeg.Data.Add dataHandler
        Async.RunSynchronously (Async.AwaitTask res)
    ///Acts just like a split but only activates at the last occurance of the input
    let splitAtLastOccurance (input:string) (splitter:string)= 
        let index= input.LastIndexOf splitter
        (input.Remove index,input.Substring (index+1))

    let splitAtFirstOccurance (input:string) (splitter:string)= 
        let index= input.IndexOf splitter
        (input.Remove index,input.Substring (index+1))
    type FtpConectionInfo={
        User:string
        Password:string
        Host:string
        Path:string
    }
    let FtpConectionInfo user pass host path= {User=user;Password=pass;Host=host;Path=path}
    
    let parseFTPstring inp =
        let conInfo, path= splitAtFirstOccurance inp "/"
        let cred ,ip= splitAtLastOccurance conInfo "@"
        let user, pass= splitAtFirstOccurance cred ":"
        printfn "building uri: host: |%s| usr %s password %s path %s" ip user pass path
        {User=user;Path=path;Host=ip;Password=pass}
        

    let test=
       (*  let inPath= "./testSource/BUNPREMIER.mxf"
        let fileName=IO.Path.GetFileName inPath
        let outPath="quantel:***REMOVED***@***REMOVED***/***REMOVED***Transfers/SSC to BUN/"+fileName
        Transcode inPath true outPath  *)
        let inp="quantel:***REMOVED***@***REMOVED******REMOVED***Transfers/SSC to BUN/"
       
        let res =parseFTPstring inp
        let coninfo, path= splitAtFirstOccurance inp "/"
        let cred ,ip= splitAtLastOccurance coninfo "@"
        let user, pass= splitAtFirstOccurance cred ":"
        printfn "|%s|" ip
       // let ip=dir.Destination.Substring ipCredSplit
        use client=new FluentFTP.FtpClient(ip,user,pass)
        client.Connect()
        //let exists=client.DirectoryExists(path.[0])