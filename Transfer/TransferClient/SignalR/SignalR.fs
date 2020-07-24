namespace TransferClient.SignalR
open System
open Microsoft.AspNetCore.SignalR.Client
module Connection=
    let mutable connection = 
        (HubConnectionBuilder())
            .WithUrl("http://localhost:5000/gameHub")
            .Build()

    let reconnect (connection :HubConnection) (error :'a) =
        connection.StartAsync()

    let Connections groupName= 
        // Create connection to game server
        connection.add_Closed(fun error -> reconnect connection error)
        // Start connection and login
        try
            connection.StartAsync().Wait()
        with
            | ex -> printfn "Connection error %s" (ex.ToString())
                    Environment.Exit(1)
    0