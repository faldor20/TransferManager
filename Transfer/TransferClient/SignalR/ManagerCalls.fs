namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
module ManagerCalls=
    
    let syncTransferData (connection:HubConnection) (userName:string) (changes:Dictionary<string, Dictionary<int,TransferData>>) =
        let task=connection.InvokeAsync("SyncTransferData",userName, changes)
        Async.AwaitTask task
    let overwriteTransferData (connection:HubConnection) (userName:string) (newData:Dictionary<string, Dictionary<int,TransferData>>) =
        let task=connection.InvokeAsync("OverwriteTransferData",userName, newData)
        Async.AwaitTask task    
    let RegisterSelf (connection:HubConnection) (userName:string) =
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName ))
    
      
