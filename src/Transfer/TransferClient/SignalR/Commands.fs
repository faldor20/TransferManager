namespace TransferClient.SignalR

open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
open System.Threading.Tasks
open TransferClient
open Microsoft.Extensions.DependencyInjection;
open Microsoft.Extensions.Logging
open Serilog
module Commands =
    
    let postconnection (connection:HubConnection) userName  baseUIData =
        async{
        Logging.debugf "'Signalr' Regestering self with clientManager"
        do! ManagerCalls.RegisterSelf connection userName
        //Here we convert the Dictionary< list> to a dictionary< dictionary>
        //let dic =
           (*  LocalDB.getlocalDB()
            |> Seq.map (fun key ->
                KeyValuePair
                    (key.Key,
                     (key.Value
                      |> Seq.mapi (fun i x -> KeyValuePair(i, x))
                      |> Dictionary)
                    )
            )
            |> Dictionary *)
        //get an updated full snapshot of the Jobs and transferData and send it off.
        let (jobs,trans)=LocalDB.AcessFuncs.GetUIData()
        Logging.debugf "'Signalr' Overwriting TransferData on ClientManager"
        do! ManagerCalls.overwriteTransferData connection userName  {baseUIData with Jobs=jobs;TransferDataList=trans}
        }

    let reconnect (connection:HubConnection) userName baseUIData ct =
        let job =
            async {
                connected<-false
                while not connected do
                    try
                        Logging.infof "'Signalr' -Attempting- to connect to clientmanager"
                        connection.StartAsync(ct).Wait();
                        Logging.infof "'Signalr' -Connected- to ClientManager"
                        Async.RunSynchronously<|Async.Sleep 1000
                        Logging.infof "'Signalr' -Regestering self, and doing intial database sync.- "
                        Async.RunSynchronously <|postconnection connection userName baseUIData
                        Logging.infof "'Signalr' -Successfully connected- to ClientManager"
                        connected<-true
                    with  ex ->  Logging.warnf "{Signalr} -Failed Connection- to ClientManager retrying in 10S. Reason= \"%A\"" ex
                    do! Async.Sleep 10000
           }

        Task.Run(fun () -> Async.RunSynchronously job)

    /// Begins a connection and registers client with the manager
    let connect managerIP userName baseUIData ct =
        async{
        Logging.info "'SignalR' Building  connection to ip= {@manIP}" managerIP
        let newConnection=
            
            (HubConnectionBuilder())
                .WithUrl(sprintf "http://%s:8085/ClientManagerHub" managerIP )
                .AddMessagePackProtocol()
                .ConfigureLogging(fun (builder:ILoggingBuilder) -> builder.AddSerilog(Logging.signalrLogger,false)|>ignore ) //<- Add this line
                .Build()
       
        // Create connection to the ClientManager Server
        newConnection.add_Closed (fun error -> reconnect newConnection userName baseUIData ct)
        ClientApi.InitManagerCalls newConnection
        // Start connection and login
        Logging.infof "'SignalR' Running connection task" 
        (reconnect newConnection userName baseUIData ct).Wait()
        connection<- Some newConnection
        return newConnection
        }

        
