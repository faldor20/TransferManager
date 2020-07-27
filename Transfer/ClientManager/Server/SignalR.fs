namespace ClientManager.Server
open System.Collections.Generic
open System
open Giraffe
open System.Threading;
open System.Threading.Tasks;
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open ClientManager.Data;
open ClientManager.Data.Types;
open FSharp.Json;
open SharedFs.SharedTypes
open Microsoft.AspNetCore.Mvc
module SignalR=
   (*  type IClientApi = 
      abstract member DataResponse : Dictionary<Guid,TransferData> -> Threading.Tasks.Task *)

    type ITransferClientApi = 
      abstract member CancelTransfer :string -> int -> System.Threading.Tasks.Task
      abstract member Testing :string -> System.Threading.Tasks.Task
      abstract member ReceiveID :int -> System.Threading.Tasks.Task

    type ClientManagerHub()=
        inherit Hub<ITransferClientApi>()

        member this.SendProgress groupName id transferData=
            
           (groupName,id)||> DataBase.setTransferData transferData
        member this.GetTransferData groupName id=
           (groupName,id)||> DataBase.getTransferData
        
        member this.RegisterSelf groupName =
           (*  //TODO: i should really use a user for this incase a transferclient has to reconnect
            this.Context.ConnectionId *)
            DataBase.registerClient groupName

        member this.RegisterNewTask groupName transferData =
            let callerID= this.Context.ConnectionId
            let DBid=groupName|>DataBase.addTransferData transferData
            DataBase.setNewTaskID groupName DBid callerID
            printfn "registered new task for group %s and id %i" (groupName) DBid
            DBid
        member this.SyncTransferData (changes:Dictionary<string, Dictionary<int,TransferData>>) =
            //printfn "syncing transferData "
            DataBaseSync.syncDataBaseChanges changes

    type IFrontendApi = 
      abstract member ReceiveData :Dictionary<string, List<TransferData>> -> System.Threading.Tasks.Task
      abstract member Testing :string -> System.Threading.Tasks.Task
    //this apprently needs to be injected
    type ClientManager (hubContext :IHubContext<ClientManagerHub,ITransferClientApi>) =
        inherit Controller ()
        member this.HubContext :IHubContext<ClientManagerHub, ITransferClientApi> = hubContext
        member this.CancelTransfer clientId  groupName id=
            hubContext.Clients.Client(clientId).CancelTransfer groupName id

    type DataHub(manager:ClientManager)=
        inherit Hub<IFrontendApi>()
        let toDictionary (map : Map<_, _>) : Dictionary<_, _> = Dictionary(map)
        let clientManager=manager

        member this.GetTransferData()=
            let data =DataBase.dataBase
            this.Clients.All.ReceiveData(data)
        member this.GetConfirmation()=
            this.Clients.All.Testing("hiya from the other side")
        member this.CancelTransfer groupName id=
            printfn "recieved Cancellation request for item %i in group %s" id groupName;
            let clientID =DataBase.getClient groupName id
            clientManager.CancelTransfer clientID groupName id
            
        
    
             





  (*    type TransferProgressService (hubContext :IHubContext<DataHub, IClientApi>) =
        inherit BackgroundService ()
      
        member this.HubContext :IHubContext<DataHub, IClientApi> = hubContext

        override this.ExecuteAsync (stoppingToken :CancellationToken) =
            let pingTimer = new System.Timers.Timer(100)
            pingTimer.Elapsed.Add(fun _ -> 
              
              let stateSerialized = serializeGameState gState
              this.HubContext.Clients.All.GameState(stateSerialized) |> ignore)

            pingTimer.Start()
            Task.CompletedTask  *)