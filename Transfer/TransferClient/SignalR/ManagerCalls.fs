namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
module ManagerCalls=


  (*   let setTransferData (newData:TransferData) (groupName:string) (index:int)=
        connection.SendAsync("SendProgress",groupName, index, newData).Wait() *)
   (*  let getTransferData(groupName:string) (index:int)=
        let task=connection.InvokeAsync<TransferData>("GetTransferData",groupName, index)
        (Async.AwaitTask task) *)
        
    
    (* let addTransferData (newData:TransferData) (groupName:string)=
        let id=connection.InvokeAsync<int>("RegisterNewTask",groupName, newData)
        Async.AwaitTask id *)

    let syncTransferData (userName:string) (changes:Dictionary<string, Dictionary<int,TransferData>>) =
        let task=connection.InvokeAsync("SyncTransferData",userName, changes)
        Async.AwaitTask task

   (*  let addTransferData newData key1=
    let getTransferData group index= dataBase.[group].[index] *)

    let RegisterSelf (userName:string) =
        Async.AwaitTask (connection.InvokeAsync ("RegisterSelf",userName ))

    (* let RequestID (groupName:string)=
      let res=connection.InvokeAsync<int>("RequestTaskID",groupName)
      let task=Async.AwaitTask res
      task *)
  (*   let rec reconnect (connection :HubConnection) (error :'a) =
        connection.StartAsync() *)


      
