namespace ClientManager.Server
open System.Collections.Generic
open System.Threading.Tasks;
open Microsoft.AspNetCore.SignalR
open ClientManager.Data;
open SharedFs.SharedTypes
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http.Features;
open FSharp.Control.Tasks.V2
open ClientManager.Config.Reader
module SignalR=
   (*  type IClientApi = 
      abstract member DataResponse : Dictionary<Guid,TransferData> -> Threading.Tasks.Task *)

    
    let uiConfig= readConfig "./Config-UI.json"

    type ITransferClientApi = 
      abstract member CancelTransfer : int -> Task
      abstract member Testing :string -> Task
      abstract member SwitchJobs:(JobID)->(JobID)->Task
      abstract member ResetDB :unit -> Task
      abstract member StartReceivingTranscode: string->Task

    and ClientManagerHub(manager:FrontEndManager)=
        inherit Hub<ITransferClientApi>()
        let frontEndManager=manager
        override this.OnDisconnectedAsync  (excep)=
            let t1 =
                task{
                    lock DataBase.dataBase (fun ()->
                        match DataBase.tryGetUserName this.Context.ConnectionId with
                        |Some(x)->
                            printfn "client with name: %s has diconnected" x
                            lock(DataBase.dataBase) (fun ()->
                            DataBase.dataBase.[x].ClientConnected<-false
                            )
                        |None-> printfn "client that has no username disconnected"
                    )
                }:>Task
            let t=base.OnDisconnectedAsync(excep);
            Task.WhenAll [t1;t]
        
        member this.GetTransferData groupName id=
           (groupName,id)||> DataBase.getTransferData

        member this.RegisterSelf (userName:string)  =
            printfn "Registering new client. Username: %s connectionID=%s userID=%s" userName this.Context.ConnectionId  this.Context.UserIdentifier 
            
            DataBase.registerClient userName this.Context.ConnectionId
            
        member this.OverwriteTransferData(userName:string) ( changes:UIData) =
            printfn "overwriting local info with client info. Username: %s connectionID=%s userID=%s" userName this.Context.ConnectionId  this.Context.UserIdentifier 
            lock(DataBase.dataBase) (fun ()->
            DataBase.dataBase.[userName]<- changes
            )

        member this.SyncTransferData (userName:string) ( changes:UIData) =
            printfn "syncing transferData "
            DataBaseSync.mergeChanges userName changes
            frontEndManager.ReceiveDataChange userName changes
            printfn "Synced transferData from %s" userName  


        member this.StartReceiver  (receiverName:string) args=
            printfn "Received request to make client '%s' run FFmpeg with args: '%s'"receiverName args 
            match DataBase.getConnectionID receiverName with 
            |Ok(connectionId)->
                printfn "starting to trigger client to receive"
                (this.Clients.Client(connectionId).StartReceivingTranscode args).Wait();
                printfn "finished triggering client %s to receive an ffmpeg stream"  receiverName
                true //TODO: get some kind of failure or sucess from reciver?
            |Error()->
                printfn "EROR: Could not find reciver in list of clients. Not sending args or triggering ffmpeg start on reciever "
                false
            
    and IFrontendApi = 
      abstract member ReceiveData :(Dictionary<string, UIData>) -> Task
      abstract member ReceiveUIConfig :UIConfig -> Task
      abstract member ReceiveDataChange :string->UIData -> Task
      abstract member Testing :string -> Task
    //this apprently needs to be injected
    and ClientManager (hubContext :IHubContext<ClientManagerHub,ITransferClientApi>) =
        inherit Controller ()
        member this.HubContext :IHubContext<ClientManagerHub, ITransferClientApi> = hubContext
        member this.CancelTransfer user id= 
            match DataBase.getConnectionID user with
            |Ok(clientID)->printfn "Sending Cancellation request to user:%s with connecionid %s" user clientID
            |_->()
            (this.HubContext.Clients.All.CancelTransfer id).Wait()
        member this.SwitchJobs  user job1 job2=

            printfn "Switching Jobs %A , %A user:%s " job1 job2 user 
            (this.HubContext.Clients.All.SwitchJobs job1 job2).Wait()
        member this.ResetDB ()=
            this.HubContext.Clients.All.ResetDB();
        

    and DataHub(manager:ClientManager)=
        inherit Hub<IFrontendApi>()
        let toDictionary (map : Map<_, _>) : Dictionary<_, _> = Dictionary(map)
        let clientManager=manager

        member this.GetTransferData()=
            let data =DataBase.dataBase
            this.Clients.All.ReceiveData(data)

        member this.GetUIConfig()=
            let config=uiConfig
            this.Clients.All.ReceiveUIConfig(config)

        member this.GetConfirmation()=
            this.Clients.All.Testing("hiya from the other side")

        member this.CancelTransfer  user id=
            printfn "recieved Cancellation request for item %i and user %s" id user ;

            clientManager.CancelTransfer  user id
        member this.SwitchJobs  (user:string) (job1:int) (job2:int)=
            clientManager.SwitchJobs  user job1 job2

    and FrontEndManager (hubContext :IHubContext<DataHub,IFrontendApi>) =
        inherit Controller ()
        member this.HubContext :IHubContext<DataHub, IFrontendApi> = hubContext
        member this.ReceiveDataChange user change=
            (this.HubContext.Clients.All.ReceiveDataChange user change).Wait()
        member this.ReceiveData change=
            (this.HubContext.Clients.All.ReceiveData DataBase.dataBase).Wait()
    
    
             





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