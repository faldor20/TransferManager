namespace TransferClient.SignalR

open System.Collections.Generic
open System
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.SignalR.Connection
open SharedFs.SharedTypes

module Commands =
    /// Begins a connection and registers the groupNames with the manager
    let MakeConnection groupNames ct =
        Connect(ct)

        let registerJobs =
            groupNames |> List.map (ManagerCalls.RegisterSelf)

        registerJobs
        |> Async.Parallel
        |> Async.RunSynchronously
        |>ignore
        ()
