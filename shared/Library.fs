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
          location:int list}

    type ScheduleID = int
    type JobID = int
    ///A list of ScheduleIDs represetnig a specific place within the jobHeirachy with the highest level id at the beginning and the deepest at the end
    type HierarchyLocation = ScheduleID list
    /// a list where each value is  a level in the hierarchy, each level contains a list of Heiracheylocations
    /// this is so that the jobHeirachy can be iterated bottom up during a shuffleup
    /// eg:       1             index=2
    ///    1,1       ;     1,2  index=1
    /// 1,1,1; 1,1,2  ; 1,2,1    index=0
    type HierarchyOrder = HierarchyLocation list list
    type FinishedJobs= list<JobID>
    type TransferDataList =Dictionary<JobID, TransferData>

    type JobHierarchy = Dictionary<HierarchyLocation, JobID list>
    [<CLIMutable>]
    type UIData = {
        mutable TransferDataList:TransferDataList
        mutable JobHierarchy:JobHierarchy
        mutable FinishedJobs:FinishedJobs
    }