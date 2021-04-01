namespace JobManager

open System.Collections.Generic
open SharedFs.SharedTypes
open FSharp.Control.Reactive
open System
open LoggingFsharp
//open System.Reactive.Linq
module Syncer=
    type SyncEvents={
        UpdateTransData:Event<TransferData* JobID>;
        FullUpdate:Event< UIJobInfo[] *TransferDataList>
    }
    let SyncEvents()=
        {UpdateTransData=new Event<TransferData*JobID>();FullUpdate=new Event<UIJobInfo[]*TransferDataList>()}
 
    


    /// Sets up Observables that invoke the provided function at the set interval using the data from the syncEvents.
    /// Intended to be used with the SyncTransferData function
    let startSyncer  (syncEvents:SyncEvents) (syncInterval:float) syncFunc (uiData:UIData)=
        //Basically we make two differnet streams, one for small incirmintal updates nd one for the full updates that come when the jobOrder changes.
        //we buffer them for the syncinterval and then run the sync.
        //TODO: it may be possible to combine both theses streams together making each generate a uidata that can then be merged
        let obs=
            syncEvents.UpdateTransData.Publish
            |>Observable.bufferSpan (TimeSpan.FromMilliseconds syncInterval)
            |>Observable.subscribe(fun x ->
                if x.Count>0 then 
                    let transDataList=
                        x
                        |>Seq.toList
                        |>List.rev //We reverse this becuase distinct keeps the first copy and we actually want the last
                        |>List.distinctBy(fun (x,y)->y)
                        |>List.map(fun (trans,jobID)-> 
                            KeyValuePair(jobID,trans))
                        |>Dictionary
                    Lgdebugf "Sending database update to ClientManager"
                    let latestData={uiData with TransferDataList= transDataList}
                    syncFunc  latestData
                )
        let obs2=
            syncEvents.FullUpdate.Publish
            |>Observable.bufferSpan (TimeSpan.FromMilliseconds syncInterval)
            |>Observable.subscribe(fun x ->
                if x.Count>0 then 
                    Lgdebugf "Sending database update to ClientManager"
                    let jobList,trans=x.[0]
                    syncFunc ({uiData with UIData.Jobs=jobList; TransferDataList=trans})
                )
        ()