namespace TransferClient.SignalR
open System
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.AspNetCore.SignalR.Protocol
open Microsoft.Extensions.DependencyInjection;

module Connection=
    let mutable connection = 
        (HubConnectionBuilder())
            .WithUrl("http://localhost:8085/ClientManagerHub")
            .AddMessagePackProtocol()
            .Build()

    let reconnect (connection :HubConnection) (error :'a) =
        connection.StartAsync()

    let Connect ct= 
        // Create connection to the ClientManager Server
        connection.add_Closed(fun error -> reconnect connection error)
        // Start connection and login
        try
            connection.StartAsync(ct).Wait()
        with
            | ex -> printfn "Connection error %s" (ex.ToString())
                    Environment.Exit(1)
        
