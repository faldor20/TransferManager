namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
type TransferDataList =Dictionary<JobID, SharedFs.SharedTypes.TransferData>
module TransferDataList =
    let set (transferDataList:TransferDataList) jobID data=
            transferDataList.[jobID]<-data
    let get (transferDataList:TransferDataList) jobID=
        transferDataList.[jobID]
    let remove (transferDataList:TransferDataList) jobID=
        transferDataList.Remove(jobID)
    type Acess =
        {
            Set:JobID ->TransferData->unit
            Get:JobID->TransferData
            Remove:JobID ->bool
        }

    let acessFuncs transDataList=
        {
            Set=set transDataList
            Get= get transDataList 
            Remove= remove transDataList
        }    
