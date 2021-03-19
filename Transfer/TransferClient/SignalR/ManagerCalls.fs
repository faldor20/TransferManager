namespace TransferClient.SignalR

open Microsoft.AspNetCore.SignalR.Client
open SharedFs.SharedTypes
open System.Net
open TransferClient
module ManagerCalls=
    
    let syncTransferData (connection:HubConnection) (userName:string) (changes:UIData) =
        let task=connection.InvokeAsync("SyncTransferData",userName, changes)
        Async.AwaitTask task
    let overwriteTransferData (connection:HubConnection) (userName:string) (newData:UIData) =
        let task=connection.InvokeAsync("OverwriteTransferData",userName, newData)
        Async.AwaitTask task    
    let RegisterSelf (connection:HubConnection) (userName:string) =   
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName))
        
    //===Transcoder====
 
    let startReceiver (connection:HubConnection) (receiverName:string) (args:string)=
        Async.AwaitTask (connection.InvokeAsync<bool> ("StartReceiver",receiverName,args))
        
      
