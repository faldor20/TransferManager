namespace SharedFs

open System

module SharedTypes =
    type TransferStatus =
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
          ID: int
          GroupKeys: list<string> }
