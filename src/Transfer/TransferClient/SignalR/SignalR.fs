namespace TransferClient.SignalR
open Microsoft.AspNetCore.SignalR.Client
 
module Connection=
    let mutable connected=false
    let mutable connection:HubConnection option=None 

    
                

        
