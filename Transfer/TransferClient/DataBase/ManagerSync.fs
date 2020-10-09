namespace TransferClient.DataBase
open TransferClient.SignalR.ManagerCalls
open TransferClient
open System.Collections.Generic
open FSharp.Control.Tasks
open Microsoft.AspNetCore.SignalR.Client
open SharedFs.SharedTypes
module ManagerSync=
    ///Sync the local database cahnges with the ClientManager main database each interval
    ///Once it is synced the changeDB will be reset.
    /// the task is started as soon as the function is called and so it does not need to be awaited or run
    let DBsyncer  (syncInterval:int) connection  userName=
        task{
            let mutable count=0
            while true do
            //TODO: i need to make sure we are actualy regeistered wih the clientManager
                if SignalR.Connection.connected then
                    let uiDat=LocalDB.jobDB.UIData
                    if uiDat.Value.NeedsSyncing then
                        Logging.debugf "Sending database update to ClientManager"
                        lock uiDat (fun ()->
                            Async.RunSynchronously (syncTransferData connection userName  !uiDat)
                            uiDat:=(UIData (!uiDat).Mapping (!uiDat).Heirachy)
                            )
                  //  count<-count+1
                do! Async.Sleep(syncInterval)
                
        }
         
        