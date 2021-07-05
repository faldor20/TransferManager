module Mover.Main 
open System
open System.IO
open SharedFs.SharedTypes;
open Mover.ProgressHandlers
open Mover.FileMove
open Mover.Types
open FTPMove

open LoggingFsharp
    
let private ffmpegMove ffmpegInfo moveJobData moveData  sourceFilePath destFilePath cb ct=
    let inType=
        match moveData.SourceFTPData with
        | Some(data)->VideoMover.InputType.FTP data
        |None ->VideoMover.InputType.File
    let func=
        match ffmpegInfo.ReceiverData with
            |Some(_)->
                match moveJobData.ReceiverFuncs with
                |Some(recv)->
                    VideoMover.sendToReceiver inType recv 
                |None->
                    Lgwarnf("'Mover' Receiver Funcs have not been set. Cannot communicate with ffmpeg reciver. This is a code issue not a configuration issue.")
                    failwith ("see above")
            |None->
                let outType=
                    match moveData.DestFTPData with
                    |Some (data)->VideoMover.OutputType.FTP data 
                    |None -> VideoMover.OutputType.File
                VideoMover.basicTranscode outType inType

    func ffmpegInfo cb sourceFilePath destFilePath ct

let private ftpMove moveData sourceFilePath destFilePath cb ct=
    let ftpFunc=
        match (moveData.SourceFTPData,moveData.DestFTPData) with
        |(Some source,Some dest)->
            FTPtoFTP source dest 
        |(Some source,None)->
            downloadFTP  source
        |(None ,Some dest)->
            uploadFTP  dest
        |(source,dest)-> 
            Lgerror3 "'Mover' Ftp progress handler was given but there is no ftp source of desitination.. SourceFTP: {@source} DestFTP: {@dest} callback : {@cb}"source dest cb
            failwith "see above"
    ftpFunc sourceFilePath destFilePath cb ct            
     
///Calls the appropriate move function based on the type of progress handler and movedata given to it
let private doMove (progressHandler:ProgressHandler) moveData (moveJobData:MoveJobData)=async{
    let fileName=IO.Path.GetFileName(moveJobData.SourcePath)
    let destFilePath= moveData.DirData.DestinationDir+fileName
    let {SourcePath= sourceFilePath;CT=ct}=moveJobData
    let result= 
        //The particular transfer action to take has allready been decided by the progress callback
        match progressHandler  with
            |FtpProg cb->
                ftpMove moveData sourceFilePath destFilePath cb ct
            |FastFileProg cb->
                FCopy sourceFilePath destFilePath cb ct
            |TranscodeProg (cb,ffmpegInfo) ->
                ffmpegMove ffmpegInfo moveJobData moveData  sourceFilePath destFilePath cb ct 
            |cb->
                Lgerror "'Mover' Progress callback not recognised. callback : {@cb}" cb
                failwith "See above"
            
    return! result 
}
///Creates a job to process a file using techniques decided apon by the info in moveData and moveJobData
///
let MoveFile moveData (moveJobData: MoveJobData)   = async {
    let {SourcePath=sourceFilePath;Transcode=transcode; CT=ct;}=moveJobData
    let {DestinationDir=destination; }=moveData.DirData
    //let isFTP=moveData.FTPData.IsSome
        
    let fileName= Path.GetFileName sourceFilePath
        
    let transData= {moveJobData.GetTransferData() with StartTime=DateTime.Now}
        
       

    let progressHandler= Gethandler moveData transcode transData (moveJobData.HandleTransferData>>Async.RunSynchronously)

    let transType=
        match progressHandler with 
        |DoubleFtpProg _->"FTP to FTP Transfer"
        |FtpProg _-> "FTP Transfer"
        |FastFileProg _->"File transfer"
        |TranscodeProg _-> "FFmpeg transcode"
    Lginfo3 " 'Mover' Starting {@type} from {@src} to {@dest}" transType sourceFilePath destination
       
    //We have to set the startTime here because we want the sartime to truly be when the task begins
    do! moveJobData.HandleTransferData (fun x-> {x with StartTime=DateTime.Now})
        
    let! result= doMove progressHandler moveData moveJobData
    //We need to dispose the sourceclient if there is one. If we getrid of this we would endlessly increase our number of active connections
       

    Lginfo2 " 'Mover' Finished copy from {@src} to {@dest}"sourceFilePath destination
    return (result,moveData.DirData.DeleteCompleted)
}
