// Learn more about F# at http://fsharp.org
namespace Transfer
open System
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Manager
open Mover
open Saturn
module Main=

    let webServer= new Task (fun () ->run  Server.Server.app)
        
    //let main2= new Task (fun()-> startUp)
    [<EntryPoint>]
    let main argv =
        printfn"Begining"
        
        (* 
        startUp
       // let a=Task.Run(fun()-> Async.RunSynchronously startUp)
        let b=Task.Run(fun()-> run  Server.Server.app)
       // a.Wait()
        b.Wait() *)
        Testing.run
        0