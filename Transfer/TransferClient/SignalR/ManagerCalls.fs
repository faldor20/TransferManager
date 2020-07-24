namespace TransferClient.SignalR

open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
module ManagerCalls=

    let setTransferData (newData:TransferData) (groupName:string) (index:int)=
        connection.SendAsync("SendProgress",groupName, index, newData).Wait()
    let getTransferData(groupName:string) (index:int)=
        let task=connection.InvokeAsync<TransferData>("GetTransferData",groupName, index)
        (Async.AwaitTask task)
        

    let addTransferData (newData:TransferData) (groupName:string)=
        let id=connection.InvokeAsync<int>("RegisterNewTask",groupName,  newData)
        Async.AwaitTask id

   (*  let addTransferData newData key1=
    let getTransferData group index= dataBase.[group].[index] *)
    






    let RegisterSelf groupName=
      connection.InvokeAsync("RegisterSelf",groupName )
    let CancelTransfer=Action<string,int>(fun  groupName id->
        //cancellation token stuff
        ()
      )

    let RequestID (groupName:string)=
      let res=connection.InvokeAsync<int>("RequestTaskID",groupName)
      let task=Async.AwaitTask res
      task
    let rec reconnect (connection :HubConnection) (error :'a) =
        connection.StartAsync()
      (*  type ITransferClientApi = 
      abstract member CancelTransfer :string -> int -> System.Threading.Tasks.Task
      abstract member Testing :string -> System.Threading.Tasks.Task
      abstract member ReceiveID :int -> System.Threading.Tasks.Task *)
    
    let InitManagerCalls groupName= 
      // Create connection to game server
      connection.On<string, int>("CancelTransfer",CancelTransfer ) |> ignore
      
