namespace TransferClient.IO
open System
open System.IO
open SharedFs.SharedTypes;
open ProgressHandlers
open FileMove
open Types
open TransferClient
open FTPMove
module Mover =
  
    ///Calls the appropriate mve function based on the type of progress hanlder and movedata given to it
    ///
    let private doMove (progressHandler:ProgressHandler) moveData (moveJobData:MoveJobData)=async{
        let fileName=IO.Path.GetFileName(moveJobData.SourcePath)
        let destFilePath= moveData.DirData.DestinationDir+fileName
        let {SourcePath= sourceFilePath;CT=ct}=moveJobData
        let result= 
            //The particular transfer action to take has allready been decided by the progress callback
            match progressHandler  with
                |FtpProg cb->
                    match (moveData.SourceFTPData,moveData.DestFTPData) with
                    |(Some source,Some dest)->
                        FTPtoFTP source dest sourceFilePath destFilePath cb ct
                    |(Some source,None)->
                        downloadFTP  source sourceFilePath destFilePath cb ct
                    |(None ,Some dest)->
                        uploadFTP  dest sourceFilePath destFilePath cb ct
                    |(source,dest)-> 
                        Logging.error3 "'Mover' Ftp progress handler was given but there is no ftp source of desitination.. SourceFTP: {@source} DestFTP: {@dest} callback : {@cb}"source dest cb
                        failwith "see above"
                |FastFileProg cb->
                    FCopy sourceFilePath destFilePath cb ct
                |TranscodeProg (cb,ffmpegInfo) ->
                    match ffmpegInfo.ReceiverData with
                       |Some(_)->
                            match moveJobData.ReceiverFuncs with
                            |Some(recv)->
                                VideoMover.sendToReceiver ffmpegInfo recv cb sourceFilePath destFilePath ct
                            |None->
                                Logging.warnf("'Mover' Receiver Funcs have not been set. Cannot communicate with ffmpeg reciver. This is a code issue not a configuration one.")
                                failwith ("see above")
                       |None->VideoMover.Transcode ffmpegInfo moveData.DestFTPData cb sourceFilePath destFilePath  ct
                |cb->
                    Logging.error "'Mover' Progress callback not recognised. callback : {@cb}" cb
                    failwith "See above"
            
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
        Logging.info3 " 'Starting' {@type} from {@src} to {@dest}" transType sourceFilePath destination
       
        //We have to set the startTime here because we want the sartime to truly be when the task begins
        moveJobData.HandleTransferData {transData with StartTime=DateTime.Now}
        
        let! result= doMove progressHandler moveData moveJobData
        //We need to dispose the sourceclient if there is one. If we getrid of this we would endlessly increase our number of active connections
       

        Logging.info2 " 'Finished' copy from {@src} to {@dest}"sourceFilePath destination
        return (result,moveData.DirData.DeleteCompleted)
    }
