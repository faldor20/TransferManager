namespace TransferClient.Database
open TransferClient.SignalR.ManagerCalls
open TransferClient
open System.Collections.Generic
open FSharp.Control.Tasks
module ManagerSync=
    ///Sync the local database cahnges with the ClientManager main database each interval
    ///Once it is synced the changeDB will be reset.
    let DBsyncer (syncInterval:int)=
        task{
            let mutable count=0
            while true do
                if LocalDB.ChangeDB.Count>0 then 
                   // printfn "Starting database sync"
                    Async.RunSynchronously (syncTransferData LocalDB.ChangeDB)
                    LocalDB.ChangeDB<-Dictionary()
                count<-count+1
                do! Async.Sleep(syncInterval)
        }
         
        