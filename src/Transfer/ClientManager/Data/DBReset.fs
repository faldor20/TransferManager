namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
open ClientManager.Server.SignalR
open Microsoft.Extensions.Hosting
open System.Threading.Tasks
module DBReset=
(*     type Resetter (frontManager:FrontEndManager,clientManager:ClientManager) =
        member this.FrontEndManager :FrontEndManager = frontManager
        member this.ClientManager :ClientManager = clientManager
        member this.StartResetWatcher=
           
        member this.ResetDB users=
             *)
    let reset (clientManager:ClientManager) (frontEndManager:FrontEndManager) =
        
        frontEndManager.ReceiveData DataBase.dataBase
        clientManager.ResetDB().Wait();
       // users|>List.iter(fun x-> (clientManager.ResetDB x).Wait())  
          
                  
    type DBResetService (frontManager:FrontEndManager ,clientManager:ClientManager) =
        inherit BackgroundService ()
        
        member this.FrontEndManager :FrontEndManager = frontManager
        member this.ClientManager :ClientManager = clientManager
       
        
        override this.ExecuteAsync (stoppingToken :CancellationToken) =
            let resetCheck args=
                    let hour=DateTime.Now.Hour
                    if hour=1 then
                        //let users=DataBase.dataBase|>Seq.collect(fun group->group.Value|>Seq.map(fun user->user.Key))|>Seq.toList
                        reset this.ClientManager this.FrontEndManager 
                        printfn "Reset list of jobs"
                    printfn "waiting for hour 1 to reset currently hour= %i" hour   
                    
                
            let timer=new Timers.Timer(1000.0*60.0*59.0)
            timer.Elapsed.Add resetCheck
            timer.Start()
            Task.CompletedTask


    

    