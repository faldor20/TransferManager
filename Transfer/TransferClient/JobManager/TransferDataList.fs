namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
module TransferDataList =
    let set (transferDataList:TransferDataList) jobID data=
        lock transferDataList (fun ()-> 
            transferDataList.[jobID]<-data
        )
    ///This sets the transferData and allso writes it to te UIData. 
    /// This allows for the UIData to only contain changes
    let setAndSync transDat (uIData:ref<UIData>) jobID data=
        set transDat jobID data
        lock UIData (fun ()->
        set uIData.Value.TransferDataList jobID data
        uIData.Value.NeedsSyncing<-true)
    
    let get (transferDataList:TransferDataList) jobID=
        transferDataList.[jobID]
    let remove (transferDataList:TransferDataList) jobID=
        lock transferDataList (fun ()-> transferDataList.Remove(jobID))
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
