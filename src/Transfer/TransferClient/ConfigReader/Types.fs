module TransferClient.ConfigReader.Types
open Mover.Types
open System.Collections.Generic
///### Configuration for a single source destination combination.
///
///How to go about sending and receving the file is determined by what optional paramaters you include.
///
///**EG:** Including **DestFTPData** and **TranscodeData** will transcode the files and send them via ftp.
type ConfigMovementData =
    { GroupList: string list
      DirData: DirectoryData
      SourceFTPData: FTPData option
      DestFTPData: FTPData option
      TranscodeData: TranscodeData option 
      SleepTime:int option
      }

//This must remain public or running the program will fail with reflection errors
type YamlData = 
    {
        ManagerIP:string;
        ClientName:string; 
        DisplayPriority:int;
        FFmpegPath:string option;
        TotalMaxJobs:int;
        GroupMaxJobs:Map<string,int>;
        WatchDirs: ConfigMovementData list ;
    }
type ConfigData={
    manIP: string 
    ClientName:string
    ///For ui related things this sets how high up the list that client will be

    DisplayPriority:int;
    FFmpegPath:string option
    FreeTokens:Dictionary<int,int>
    SourceIDMapping:Dictionary<string,int>
    WatchDirs:WatchDir list
}