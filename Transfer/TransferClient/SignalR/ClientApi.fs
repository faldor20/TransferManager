namespace TransferClient.SignalR
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client;
module ClientApi=
    let CancelTransfer=Action<string,int>(fun  groupName id->
        printfn "Cancellation request recieved for group %s id %i" groupName id
        TokenDatabase.cancelToken groupName id
      )

    let ResetDB=Action(fun ()->
        printfn "reste request recieved"
        LocalDB.reset()
        ()
      ) 

    let InitManagerCalls (connection:HubConnection)= 
        
        printfn("[Setup] Initialising Client Signalr Triggers (connection.On...etc)")

        let types= [|string.GetType();  int.GetType()|]
        connection.On<string,int>("CancelTransfer",CancelTransfer )|>ignore
        connection.On("ResetDB",ResetDB) |>ignore