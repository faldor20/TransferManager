

module Mover.Types 
open System.Threading
open SharedFs.SharedTypes
type TransferResult =
    | Success = 0
    | Failed = 1
    | Cancelled = 2

    type ffmpegProtocol={
        ProtocolArgs:string;
        Protocoll:string;
    }
    type CustomTCPOptions={
        SendBuffer:int;
    }
    type FFmpegSendMethod=
    |CustomTCP of CustomTCPOptions
    |InbuiltCustom of ffmpegProtocol
    ///**Configuration relating to a receivr of an ffmpeg stream**
    /// 
    ///This should be included in the TranscodeData when you intend to have another 
    ///ffmpeg instance recieve the data. 
    ///
    ///**EG:** When one instance outputs a tcp stream and the other recives it. 
    /// 
    ///This allows for using a different encoding as a kind of transport stream. 
    /// 
    /// **eg:** long-gop for sending and intra for editing at both ends
type ReceiverData=
    {
    ReceivingClientName:string;
    SendMethod:FFmpegSendMethod;
    ReceivingFFmpegArgs:string;
    DynamicPort:bool;
    Port:int;
    }
        
    ///Configuration for Transcoding using ffmpeg.
    ///**ReceiverData:** Optional and should be left out if there is not a reciving ffmpeg instance
type TranscodeData =
    {   
        FfmpegArgs: string option
        OutputFileExtension: string option
        ReceiverData:ReceiverData option 
        TranscodeExtensions: string list
    }

let TranscodeData  ffmpegArgs outputFileExtension receiverData transcodeExtensions =
    {   TranscodeExtensions = transcodeExtensions
        FfmpegArgs = ffmpegArgs
        OutputFileExtension = outputFileExtension
        ReceiverData=receiverData }
    
type FTPData =
    {   User: string
        Password: string
        Host: string }

let FTPData user password host =
    {   User = user
        Password = password
        Host = host }
    
type DirectoryData =
    { 
        SourceDir: string
        DestinationDir: string
        DeleteCompleted: bool }

let DirectoryData groupName source destination deleteCompleted =
    {
        SourceDir = source
        DestinationDir = destination
        DeleteCompleted = deleteCompleted }

type MovementData =
    {   GroupList: int list
        DirData: DirectoryData
        SourceFTPData: FTPData option
        DestFTPData: FTPData option
        TranscodeData: TranscodeData option
        //TODO: This might want to be moved into a record of some sort
        SleepTime:int option }
///**Functions used to interact with the receiver of an ffmpeg stream.**
///
///These exist so that the "Mover" and "VideoMover" parts don't have to 
///know the specifics of how the communication is done.
///this leaves it open to use http, serial, grpc or whatever else
type ReceiverFuncs={
    ///string1 is the receiverName string2 is the receiverArgs
    StartTranscodeReciever:(string->string->Async<bool>)

}         
/// **Start Transcode Reciever:** A function that is called to trigger the start of an ffmpeg instance on another machine that waits for incoming data
type MoveJobData={
    SourcePath:string;
    Transcode:bool;
    CT:CancellationToken;
    GetTransferData:unit->TransferData
    HandleTransferData:TransferData->unit
    ReceiverFuncs:ReceiverFuncs option
      
}

type MoveJob=Async<TransferResult * int * CancellationToken>
type ScheduledTransfer = Async<MoveJob>
type WatchDir =
    {   MovementData: MovementData
        TransferedList: string list
        ScheduledTasks: ScheduledTransfer list }

type FoundFile =
    {   Path: string
        FTPFileInfo: FluentFTP.FtpListItem option }
