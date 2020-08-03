namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
module ManagerCalls=

    let syncTransferData (userName:string) (changes:Dictionary<string, Dictionary<int,TransferData>>) =
        let task=connection.InvokeAsync("SyncTransferData",userName, changes)
        Async.AwaitTask task
        
    let RegisterSelf (userName:string) =
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName ))

      
