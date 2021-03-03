namespace TransferClient.SignalR
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client;
open TransferClient
open TransferClient.IO;
open Types;
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
 (*    let GetIP=(fun (a:string)->
        task{
            let ipHostInfo = Dns.GetHostEntry(Dns.GetHostName()); // `Dns.Resolve()` method is deprecated.
            let ipAddress = ipHostInfo.AddressList.[0];

            return ipAddress.ToString();
       }
    )  *)
    let StartReceivingTranscode=Action<string>(fun args->
        match VideoMover.startReceiving args|>Async.RunSynchronously with
        |TransferResult.Success-> Logging.infof"{SignalR} Receiving data from ffmpeg stream was sucessful";
        |_->Logging.warnf "{SignalR} Receiving data from FFmpeg failed."
    )

    let InitManagerCalls (connection:HubConnection)= 
        
        Logging.infof ("{ClientAPI} Initialising Client Signalr Triggers (connection.On...etc)")

        let types= [|string.GetType();  int.GetType()|]

        connection.On<int>("CancelTransfer",CancelTransfer )|>ignore
        connection.On("ResetDB",ResetDB) |>ignore
        connection.On("SwitchJobs",SwitchJobs)|>ignore
        connection.On("StartReceivingTranscode",StartReceivingTranscode)|>ignore
        (* connection.On("GetIP",GetIP)|>ignore *)
        