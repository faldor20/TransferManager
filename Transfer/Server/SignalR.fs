namespace Transfer
open System.Collections.Generic
open System
open Giraffe
open System.Threading;
open System.Threading.Tasks;
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open Data;
open FSharp.Json;
module SingnalR=
   (*  type IClientApi = 
      abstract member DataResponse : Dictionary<Guid,TransferData> -> Threading.Tasks.Task *)

    type DataHub()=
        inherit Hub()
        let toDictionary (map : Map<_, _>) : Dictionary<_, _> = Dictionary(map)
        member this.GetTransferData()=
            let data =Data.dataBase
          
            this.Clients.All.SendAsync("ReceiveData",toDictionary(data))
        member this.GetConfirmation()=
            this.Clients.All.SendAsync("Testing","hiya from the other side")
        member this.CancelTransfer groupName id=
            printfn "recieved Cancellation request for item %i in group %s" id groupName;
            Data.CancellationTokens.[groupName].[id].Cancel()


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