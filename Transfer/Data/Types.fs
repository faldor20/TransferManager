namespace Transfer.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
open IOExtensions;
module Types=
    type ScheduledTransfer=Async<Async<TransferResult*int*CancellationToken>>
   
   
    type TranscodeData=
        {
            TranscodeExtensions:string list;
            FfmpegArgs:string option;
            OutputFileExtension:string option;
        }
    let TranscodeData  transcodeExtensions ffmpegArgs outputFileExtension = {TranscodeExtensions= transcodeExtensions; FfmpegArgs=ffmpegArgs;OutputFileExtension =outputFileExtension}
    type FTPData={
        User:string
        Password:string
        Host:string
    }
    let FTPData  user password host= {User= user; Password=password; Host=host}
    type DirectoryData={
        GroupName:string
        SourceDir: string
        DestinationDir: string
    }
    let DirectoryData groupName source destination ={GroupName=groupName;SourceDir=source;DestinationDir=destination;}
    type MovementData={
        DirData:DirectoryData
        FTPData:FTPData option;
        TranscodeData:TranscodeData option;
    }
    type WatchDir =
        { 
          MovementData:MovementData
          TransferedList: string list
          ScheduledTasks:ScheduledTransfer list;
          }
    