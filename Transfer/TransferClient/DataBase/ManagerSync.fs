namespace TransferClient.DataBase
open TransferClient.SignalR.ManagerCalls
open TransferClient
open System.Collections.Generic
open FSharp.Control.Tasks
open Microsoft.AspNetCore.SignalR.Client
module ManagerSync=
    ///Sync the local database cahnges with the ClientManager main database each interval
    ///Once it is synced the changeDB will be reset.
    let DBsyncer (syncInterval:int) userName=
        task{
            let mutable count=0
            while true do
            //TODO: i need to make sure we are actualy regeistered wih the clientManager
                if SignalR.Connection.connected then
                    if LocalDB.ChangeDB.Count>0 then 

                        Async.RunSynchronously (syncTransferData userName LocalDB.ChangeDB)
                        LocalDB.ChangeDB<-Dictionary()
                    count<-count+1
                do! Async.Sleep(syncInterval)
                
        }
         
        