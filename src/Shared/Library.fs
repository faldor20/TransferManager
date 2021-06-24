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

    type ColourScheme=
       |Normal=0
       |Alt=1
    [<CLIMutable>]
    type UIConfig=
        {
            ColourScheme:ColourScheme
            Heading:string
            SideHeading:string

        }

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
        mutable ClientConnected:bool
        TransferDataList:TransferDataList
        mutable Jobs:UIJobInfo array
        Heirachy:Dictionary<int,List<int>> array
        ///For ui related things this sets how high up the list that client will be
        DisplayPriority:int;
    }
    let UIData displayPriority mapping heirachy={DisplayPriority =displayPriority;UIData.Jobs=Array.empty;UIData.TransferDataList=TransferDataList();Mapping=mapping; Heirachy=heirachy; ClientConnected =true; }