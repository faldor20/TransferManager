namespace SharedFs
open System.Collections.Generic
open System

module SharedTypes =
    type TransferStatus =
        | Unavailable=0
        | Waiting = 1
        | Copying = 2
        | Complete = 3
        | Cancelled = 4
        | Failed = 5

    type TransferTypes =
        | FTPtoFTP = 0
        | FTPtoLocal = 1
        | LocaltoFTP = 2
        | LocaltoLocal = 3
    [<CLIMutable>]
    type TransferData =
        { Percentage: float
          ///MB
          FileSize: float
          ///MB
          FileRemaining: float
          Speed: float
          Destination: string
          Source: string
          Status: TransferStatus
          ScheduledTime: DateTime
          TransferType: TransferTypes
          StartTime: DateTime
          EndTime: DateTime
          jobID:int
          location:int array}
//=========jobDB====
    type SourceID = int
    type JobID = int
    type TransferDataList =Dictionary<JobID, TransferData>
    type UIJobInfo={JobID:JobID;RequiredTokens:SourceID array}
    ///Data to be sent to the UI by the clientManager
    /// 
    [<CLIMutable>]
    type UIData= {
        Mapping:Dictionary<int,string>
        TransferDataList:TransferDataList
        mutable Jobs:UIJobInfo array
        Heirachy:Dictionary<int,List<int>> array
    }
    let UIData mapping heirachy={UIData.Jobs=Array.empty;UIData.TransferDataList=TransferDataList();Mapping=mapping; Heirachy=heirachy; }