namespace TransferClient.SignalR
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client;
open TransferClient

module ClientApi=
    let CancelTransfer=Action<int>(fun  id->
        Logging.debugf "Cancellation request recieved for id %i"  id
        TokenDatabase.cancelToken id
      )

    let ResetDB=Action(fun ()->
        Logging.debugf "reset request recieved"
        LocalDB.reset()
        ()
      ) 

    let InitManagerCalls (connection:HubConnection)= 
        
        Logging.infof ("{ClientAPI} Initialising Client Signalr Triggers (connection.On...etc)")

        let types= [|string.GetType();  int.GetType()|]
        connection.On<int>("CancelTransfer",CancelTransfer )|>ignore
        connection.On("ResetDB",ResetDB) |>ignore