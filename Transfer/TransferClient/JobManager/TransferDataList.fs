namespace TransferClient.JobManager
open System.Collections.Generic
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
    type Acess =
        {
            //Set:JobID ->TransferData->unit
            SetAndSync:JobID ->TransferData->unit
            Get:JobID->TransferData
            Remove:JobID ->bool
        }

    let acessFuncs transDataList uiData=
        {
           // Set=set transDataList
            SetAndSync=setAndSync transDataList uiData
            Get= get transDataList 
            Remove= remove transDataList
        }    
