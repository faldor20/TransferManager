namespace TransferClient.IO
open System.Runtime
open System
open System.IO
open System.Threading
open System.Diagnostics
open System.Threading.Tasks
open FSharp.Control.Tasks
open SharedFs.SharedTypes;
open FluentFTP
open ProgressHandlers
open TransferClient.DataBase.Types
open FileMove
open Types
open TransferClient
open TransferClient.JobManager.Access;
open TransferClient.JobManager.Main
open FTPMove
module Mover =
  
    
    let doMove (progressHandler:ProgressHandler) moveData sourceFilePath destination fileName (ct:CancellationToken)=async{
        let result= 
            //The particular transfer action to take has allready been decided by the progress callback
            match (moveData.SourceFTPData,moveData.DestFTPData, progressHandler)  with
                |  (Some source,Some dest,FtpProg cb)         ->
                    FTPtoFTP source dest sourceFilePath (destination+fileName) cb ct
                |Some source,None,FtpProg cb->
                    downloadFTP  source sourceFilePath (destination+fileName) cb ct
                |(None ,Some dest,FtpProg cb)->
                    uploadFTP  dest sourceFilePath (destination+fileName) cb ct
                |(None,None,FastFileProg cb)->
                    FCopy sourceFilePath (destination+fileName) cb ct
                |source,dest,cb->
                    Logging.errorf "{Mover}Some combination of inputs made the mover not able to run. SourceFTP: %A DestFTP: %A callback : %A"source dest cb
                    failwith "See above"
                //|TranscodeProg (cb, ffmpegInfo) -> VideoMover.Transcode ffmpegInfo moveData.FTPData cb filePath destination ct.Token
        return! result 
    }

    let MoveFile moveData (moveJobData: MoveJobData)   = async {
        let {SourcePath=sourceFilePath;Transcode=transcode; CT=ct;}=moveJobData
        let {DestinationDir=destination; }=moveData.DirData
        //let isFTP=moveData.FTPData.IsSome
        
        let fileName= Path.GetFileName sourceFilePath
        
        let transData= {moveJobData.GetTransferData() with StartTime=DateTime.Now}
        
       

        let progressHandler= Gethandler moveData transcode transData moveJobData.HandleTransferData

        let transType=
            match progressHandler with 
            |DoubleFtpProg _->"FTP to FTP Transfer"
            |FtpProg _-> "FTP Transfer"
            |FastFileProg _->"File transfer"
            |TranscodeProg _-> "FFmpeg transcode"
        Logging.infof " {Starting} %s from %s to %s" transType sourceFilePath destination
       
        //We have to set the startTime here because we want the sartime to truly be when the task begins
        moveJobData.HandleTransferData {transData with StartTime=DateTime.Now}
        
        let! result= doMove progressHandler moveData sourceFilePath destination fileName ct
        //We need to dispose the sourceclient if there is one. If we getrid of this we would endlessly increase our number of active connections
       

        Logging.infof " {Finished} copy from %s to %s"sourceFilePath destination
        return (result,moveData.DirData.DeleteCompleted)
    }
