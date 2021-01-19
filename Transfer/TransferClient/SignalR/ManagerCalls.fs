namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
open TransferClient.JobManager
module ManagerCalls=
    
    let syncTransferData (connection:HubConnection) (userName:string) (changes:UIData) =
        let task=connection.InvokeAsync("SyncTransferData",userName, changes)
        Async.AwaitTask task
    let overwriteTransferData (connection:HubConnection) (userName:string) (newData:UIData) =
        let task=connection.InvokeAsync("OverwriteTransferData",userName, newData)
        Async.AwaitTask task    
    let RegisterSelf (connection:HubConnection) (userName:string) =
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName ))
    
      
