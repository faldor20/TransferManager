namespace TransferClient.IO

open System.Threading

module Types =
    type TransferResult =
        | Success = 0
        | Failed = 1
        | Cancelled = 2

    type ScheduledTransfer = Async<Async<TransferResult * int * CancellationToken>>


    type TranscodeData =
        { TranscodeExtensions: string list
          FfmpegArgs: string option
          OutputFileExtension: string option }
    ///
    let TranscodeData transcodeExtensions ffmpegArgs outputFileExtension =
        { TranscodeExtensions = transcodeExtensions
          FfmpegArgs = ffmpegArgs
          OutputFileExtension = outputFileExtension }

    type FTPData =
        { User: string
          Password: string
          Host: string }

    let FTPData user password host =
        { User = user
          Password = password
          Host = host }

    type DirectoryData =
        { GroupName: string
          SourceDir: string
          DestinationDir: string
          DeleteCompleted: bool }

    let DirectoryData groupName source destination deleteCompleted =
        { GroupName = groupName
          SourceDir = source
          DestinationDir = destination
          DeleteCompleted = deleteCompleted }

    type MovementData =
        { DirData: DirectoryData
          SourceFTPData: FTPData option
          DestFTPData: FTPData option
          TranscodeData: TranscodeData option }

    type WatchDir =
        { MovementData: MovementData
          TransferedList: string list
          ScheduledTasks: ScheduledTransfer list }

    type FoundFile =
        { Path: string
          FTPFileInfo: FluentFTP.FtpListItem option }
