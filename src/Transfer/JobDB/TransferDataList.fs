namespace JobManager
open SharedFs.SharedTypes
open LoggingFsharp
module TransferDataList =
    let set (transferDataList:TransferDataList) jobID data=
        (* TransferClient.Logging.infof " %A %A %A"transferDataList jobID data *)
       
        transferDataList.[jobID]<-data
        
            
  
    ///This sets the transferData and allso writes it to te UIData. 
    /// This allows for the UIData to only contain changes
    let updateAndSync (transDat:TransferDataList) (syncer:Syncer.SyncEvents) jobID updateFunc=
        let data=transDat.[jobID] 
        let newData=(updateFunc data )
        set transDat jobID newData
        syncer.UpdateTransData.Trigger (newData,jobID)
    let setAndSync (transDat:TransferDataList) (syncer:Syncer.SyncEvents) jobID newData=
        set transDat jobID newData
        syncer.UpdateTransData.Trigger (newData,jobID) 
    
    let get (transferDataList:TransferDataList) jobID=
        transferDataList.[jobID]
    let remove (transferDataList:TransferDataList) jobID=transferDataList.Remove(jobID)

