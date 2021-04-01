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
open LoggingFsharp
open Logging
module Commands =
    
    let postconnection (connection:HubConnection) userName  baseUIData =
        async{
        Lgdebugf "'Signalr' Regestering self with clientManager"
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
        Lgdebugf "'Signalr' Overwriting TransferData on ClientManager"
        do! ManagerCalls.overwriteTransferData connection userName  {baseUIData with Jobs=jobs;TransferDataList=trans}
        }

    let reconnect (connection:HubConnection) userName baseUIData ct =
        let job =
            async {
                connected<-false
                while not connected do
                    try
                        Lginfof "'Signalr' -Attempting- to connect to clientmanager"
                        connection.StartAsync(ct).Wait();
                        Lginfof "'Signalr' -Connected- to ClientManager"
                        Async.RunSynchronously<|Async.Sleep 1000
                        Lginfof "'Signalr' -Regestering self, and doing intial database sync.- "
                        Async.RunSynchronously <|postconnection connection userName baseUIData
                        Lginfof "'Signalr' -Successfully connected- to ClientManager"
                        connected<-true
                    with  ex ->  Lgwarnf "{Signalr} -Failed Connection- to ClientManager retrying in 10S. Reason= \"%A\"" ex
                    do! Async.Sleep 10000
           }

        Task.Run(fun () -> Async.RunSynchronously job)

    /// Begins a connection and registers client with the manager
    let connect managerIP userName baseUIData ct =
        async{
        Lginfo "'SignalR' Building  connection to ip= {@manIP}" managerIP
        let newConnection=
            
            (HubConnectionBuilder())
                .WithUrl(sprintf "http://%s:8085/ClientManagerHub" managerIP )
                .AddMessagePackProtocol()
                .ConfigureLogging(fun (builder:ILoggingBuilder) -> builder.AddSerilog(signalrLogger,false)|>ignore ) //<- Add this line
                .Build()
       
        // Create connection to the ClientManager Server
        newConnection.add_Closed (fun error -> reconnect newConnection userName baseUIData ct)
        ClientApi.InitManagerCalls newConnection
        // Start connection and login
        Lginfof "'SignalR' Running connection task" 
        (reconnect newConnection userName baseUIData ct).Wait()
        connection<- Some newConnection
        return newConnection
        }

        
