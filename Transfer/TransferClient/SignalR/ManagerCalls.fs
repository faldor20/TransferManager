namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
open TransferClient.JobManager
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
        let ipHostInfo = Dns.GetHostEntry(Dns.GetHostName()); // `Dns.Resolve()` method is deprecated.
        let rec ipAddress (ips:IPAddress list)=
            match ips with
            |head::tail->
                match head.AddressFamily with
                |Sockets.AddressFamily.InterNetwork->head.ToString()
                |_->ipAddress tail
            |[]->"ERRR, ran out of ip's to check wiythout finding ipv4"
        let addr=ipAddress (ipHostInfo.AddressList|>Array.toList)
        Logging.infof "registering with ip :%s" addr
        
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName,addr ))
        
    //===Transcoder====
    let getReceiverIP (connection:HubConnection)  (receiverName:string) =
        Async.AwaitTask (connection.InvokeAsync<string> ("GetReceiverIP",receiverName ))
    let startReceiver (connection:HubConnection) (receiverName:string) (args:string)=
        Async.AwaitTask (connection.InvokeAsync<bool> ("StartReceiver",receiverName,args))
        
      
