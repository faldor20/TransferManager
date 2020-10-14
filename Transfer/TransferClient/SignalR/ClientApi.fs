namespace TransferClient.SignalR
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client;
open TransferClient
open SharedFs.SharedTypes

module ClientApi=
    let CancelTransfer=Action<int>(fun  id->
        Logging.debugf "Cancellation request recieved for id %i"  id
        LocalDB.AcessFuncs.CancelJob id
      )

    let ResetDB=Action(fun ()->
        Logging.debugf "reset request recieved"
        LocalDB.reset()
        ()
      ) 
    let SwitchJobs=Action<int,int>(fun job1 job2->
        Logging.infof "Switching jobs %A and %A "job1 job2
        LocalDB.AcessFuncs.SwitchJobs job1 job2
        ()
    ) 

    let InitManagerCalls (connection:HubConnection)= 
        
        Logging.infof ("{ClientAPI} Initialising Client Signalr Triggers (connection.On...etc)")

        let types= [|string.GetType();  int.GetType()|]
        connection.On<int>("CancelTransfer",CancelTransfer )|>ignore
        connection.On("ResetDB",ResetDB) |>ignore
        connection.On("SwitchJobs",SwitchJobs)|>ignore