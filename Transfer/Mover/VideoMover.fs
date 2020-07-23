namespace Transfer
open System
open FFmpeg.NET
open IOExtensions
open Data.Types
module VideoMover=

    /// outPath should either be a straight filepath or an FTp path 
    let Transcode ffmpegInfo (ftpInfo:FTPData option) progressHandler (filePath:string)  (outPath:string) ct=
        async{
        let mpeg = new Engine("./ffmpeg.exe")
        let fileName= IO.Path.GetFileName( filePath)

        // We have to get the source files duration becuase the totalDuration given as part of the progressargs object doesnt work
        let mediaFile=MediaFile(filePath)
        let! metaData= Async.AwaitTask( mpeg.GetMetaDataAsync(mediaFile))
        
        mpeg.Progress.Add (progressHandler metaData.Duration )
        //This is all error handling
        let mutable transferError=false
        let errorHandler (errorArgs:Events.ConversionErrorEventArgs)=
            transferError<-true
            
            printfn "[ERROR] ffmpeg transcode transfer for source: %s failed with error %A" errorArgs.Input.FileInfo.Name errorArgs.Exception        
        mpeg.Error.Add errorHandler
       (*  let dataHandler (dataArgs:Events.ConversionDataEventArgs)=
            printfn "%A" dataArgs.Data        

        mpeg.Data.Add dataHandler *)

        // We woudn't normally be abe to use mp4 becuase i isn't streamable but with the right flags we can make it work
        
        let correctPath=
            match ffmpegInfo.OutputFileExtension with 
            |Some x->IO.Path.ChangeExtension((outPath+fileName),x) 
            |None->IO.Path.ChangeExtension((outPath+fileName),"mp4") 
        let out=
            match ftpInfo with
            |Some x-> "ftp://"+ x.User+":"+x.Password+"@"+x.Host+correctPath 
            |None-> correctPath


        let defaultArgs=" -c:v h264 -crf 20 -pix_fmt + -preset faster -flags +ildct+ilme  "
        let usedFFmpegArgs=
            match ffmpegInfo.FfmpegArgs with
            |Some x->x
            |None->defaultArgs
        let args="-i "+"\""+filePath+"\""+usedFFmpegArgs+"\""+out+"\"" 

        printfn "call ffmpeg with args: %s" args
        let task=mpeg.ExecuteAsync( args,ct)
        let! fin= Async.AwaitTask task
        if transferError then return TransferResult.Failed
        else if ct.IsCancellationRequested then return TransferResult.Cancelled
        else return TransferResult.Success
        }