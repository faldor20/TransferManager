namespace TransferClient.JobManager
open SharedFs.SharedTypes
module TransferDataList =
    let set (transferDataList:TransferDataList) jobID data=
        (* TransferClient.Logging.infof " %A %A %A"transferDataList jobID data *)
       
        transferDataList.[jobID]<-data
        
            
  
    ///This sets the transferData and allso writes it to te UIData. 
    /// This allows for the UIData to only contain changes
    let setAndSync transDat (syncer:Syncer.SyncEvents) jobID data=
        set transDat jobID data
        syncer.UpdateTransData.Trigger (data,jobID)
       
    
    let get (transferDataList:TransferDataList) jobID=
        transferDataList.[jobID]
    let remove (transferDataList:TransferDataList) jobID=transferDataList.Remove(jobID)

