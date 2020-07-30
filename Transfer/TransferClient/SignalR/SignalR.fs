namespace TransferClient.SignalR
open System
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.AspNetCore.SignalR.Protocol
open Microsoft.Extensions.DependencyInjection;

 open System.Threading.Tasks
 
module Connection=
    let mutable connected=false
    let mutable connection = 
        (HubConnectionBuilder())
            .WithUrl("http://localhost:8085/ClientManagerHub")
            .AddMessagePackProtocol()
            .Build()
 
                

        
