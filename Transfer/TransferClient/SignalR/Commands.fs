namespace TransferClient.SignalR

open System.Collections.Generic
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes
open System.Threading.Tasks
module Commands =
    
    let postconnection userName groupNames =
        Async.RunSynchronously(ManagerCalls.RegisterSelf userName)
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

        ManagerCalls.syncTransferData userName (dic)

    let reconnect userName groupNames ct =
        let job =
            async {
                connected<-false
                while not connected do
                    try
                        printfn "Attempting to connect to clientmanager"
                        connection.StartAsync(ct).Wait()
                        printfn "Succsefully connected to ClientManager"
                        Async.RunSynchronously (postconnection userName groupNames)
                        connected<-true
                    with  ex -> printfn "Failed to connect to ClientManager retrying in 10S Reason: %s" ex.Message
                    do! Async.Sleep 10000
           }

        Task.Run(fun () -> Async.RunSynchronously job)

    let connect userName groupNames ct =
        // Create connection to the ClientManager Server
        connection.add_Closed (fun error -> reconnect userName groupNames ct)
        ClientApi.InitManagerCalls
        // Start connection and login
        (reconnect userName groupNames ct).Wait()
    /// Begins a connection and registers the groupNames with the manager
    let MakeConnection userName groupNames ct =
        async {
            do connect userName groupNames ct

            ()
        }
