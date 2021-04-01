namespace TransferClient.SignalR
open System
open TransferClient.DataBase
open Microsoft.AspNetCore.SignalR.Client;
open TransferClient
open Mover;
open Types;
open LoggingFsharp
module ClientApi=
    let CancelTransfer=Action<int>(fun  id->
        Lgdebug "'SignalR' Cancellation request recieved for id {@id}"  id
        LocalDB.AcessFuncs.CancelJob id
    )

    let ResetDB=Action(fun ()->
        Lgdebugf "'SignalR' reset request recieved"
        LocalDB.reset()
        ()
    ) 
    let SwitchJobs=Action<int,int>(fun job1 job2->
        Lginfo2 "'SignalR' Switching jobs {@job1} and {@job2} "job1 job2
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
        try
            match VideoMover.startReceiving args|>Async.RunSynchronously with
            |TransferResult.Success-> Lginfof "[SignalR] Receiving data from ffmpeg stream was sucessful"
            |_->Lgwarnf "'SignalR' Receiving data from FFmpeg failed."
        with|e-> Lgerror "'SignalR' Exception whils receivng ffmpeg stream: Reason: {@excp}" e
    )

    let InitManagerCalls (connection:HubConnection)= 
        
        Lginfof "'ClientAPI' Initialising Client Signalr Triggers (connection.On...etc)"

        let types= [|string.GetType();  int.GetType()|]

        connection.On<int>("CancelTransfer",CancelTransfer )|>ignore
        connection.On("ResetDB",ResetDB) |>ignore
        connection.On("SwitchJobs",SwitchJobs)|>ignore
        connection.On("StartReceivingTranscode",StartReceivingTranscode)|>ignore
        (* connection.On("GetIP",GetIP)|>ignore *)
        