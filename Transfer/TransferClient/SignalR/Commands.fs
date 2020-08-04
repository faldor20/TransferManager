namespace TransferClient.SignalR

open System.Collections.Generic
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
open System.Threading.Tasks
open TransferClient
open Microsoft.AspNetCore.SignalR.Protocol
open Microsoft.Extensions.DependencyInjection;
module Commands =
    
    let postconnection (connection:HubConnection) userName groupNames =
        Async.RunSynchronously(ManagerCalls.RegisterSelf connection userName)
        //Here we convert the Dictionary< list> to a dictionary< dictionary>
        let dic =
            LocalDB.localDB
            |> Seq.map (fun key ->
                KeyValuePair
                    (key.Key,
                     (key.Value
                      |> Seq.mapi (fun i x -> KeyValuePair(i, x))
                      |> Dictionary)
                    )
            )
            |> Dictionary

        ManagerCalls.overwriteTransferData connection userName (dic)

    let reconnect (connection:HubConnection) userName groupNames ct =
        let job =
            async {
                connected<-false
                while not connected do
                    try
                        Logging.infof "{Signalr} -Attempting- to connect to clientmanager"
                        connection.StartAsync(ct).Wait()
                        Logging.infof "{Signalr} -Connecting- to ClientManager"
                        Async.RunSynchronously (postconnection connection userName groupNames)
                        Logging.infof "{Signalr} -Successfully connected- to ClientManager"
                        connected<-true
                    with  ex ->  Logging.warnf "{Signalr} -Failed Connection- to ClientManager retrying in 10S. Reason= \"%s\"" ex.Message
                    do! Async.Sleep 10000
           }

        Task.Run(fun () -> Async.RunSynchronously job)

    /// Begins a connection and registers client with the manager
    let connect managerIP userName groupNames ct =
        async{
        Logging.infof "{SignalR} Building  connection to ip= %s" managerIP
        let newConnection=
            (HubConnectionBuilder())
                .WithUrl(sprintf "http://%s:8085/ClientManagerHub" managerIP )
                .AddMessagePackProtocol()
                .Build()
       
        // Create connection to the ClientManager Server
        newConnection.add_Closed (fun error -> reconnect newConnection userName groupNames ct)
        ClientApi.InitManagerCalls newConnection
        // Start connection and login
        Logging.infof "{SignalR} Running connection task" 
        (reconnect newConnection userName groupNames ct).Wait()
        connection<- Some newConnection
        return newConnection
        }

        
