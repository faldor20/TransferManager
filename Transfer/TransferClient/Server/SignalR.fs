namespace TransferClient
open System.Collections.Generic
open System
open Giraffe
open System.Threading;
open System.Threading.Tasks;
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open TransferClient.Data;
open Data.Types;
open FSharp.Json;
open SharedFs.SharedTypes
module SingnalR=
   (*  type IClientApi = 
      abstract member DataResponse : Dictionary<Guid,TransferData> -> Threading.Tasks.Task *)
    type IClientApi = 
      abstract member ReceiveData :Dictionary<string, List<TransferData>> -> System.Threading.Tasks.Task
      abstract member Testing :string -> System.Threading.Tasks.Task
    type DataHub()=
        inherit Hub<IClientApi>()
        let toDictionary (map : Map<_, _>) : Dictionary<_, _> = Dictionary(map)

        member this.GetTransferData()=
            let data =DataBase.dataBase
            this.Clients.All.ReceiveData(toDictionary(data))
        member this.GetConfirmation()=
            this.Clients.All.Testing("hiya from the other side")
        member this.CancelTransfer groupName id=
            printfn "recieved Cancellation request for item %i in group %s" id groupName;
            DataBase.CancellationTokens.[groupName].[id].Cancel()

     
    type TransferClientHub()=
        inherit Hub()
        let toDictionary (map : Map<_, _>) : Dictionary<_, _> = Dictionary(map)

        member this.GetTransferData()=
            let data =DataBase.dataBase
            this.Clients.All.(toDictionary(data))
        member this.BeginTransfer groupName transcodeData=
            this.Clients.Client()("hiya from the other side")
        member this.CancelTransfer groupName id=
            this.Clients.Client(groupName).CancelTransfer id


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