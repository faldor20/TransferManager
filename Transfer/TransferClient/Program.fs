// Learn more about F# at http://fsharp.org
namespace TransferClient
open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Manager
open Saturn
module Main=

    //let webServer= new Task (fun () ->run Server.Server.app)
        
    //let main2= new Task (fun()-> startUp)
    [<EntryPoint>]
    let main argv =
        printfn"Begining"
        
        
    
        let a=Task.Run(fun()-> Async.RunSynchronously startUp)
        //let b=Task.Run(fun()-> run  Server.Server.app)
       // b.Wait()
        a.Wait()  
 
      
       // Testing.test
        0