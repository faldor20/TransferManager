// Learn more about F# at http://fsharp.org
namespace ClientManager
open System
open Data.DataBase
open System.Threading.Tasks
open Saturn
module Main=
    [<EntryPoint>]
    let main argv =
        printfn "Hello World from F#!"
        //Start the task that clears the history of transfers at 00:00
        Async.Start(resetWatch)
        let b=Task.Run(fun()->run Server.Server.app)
        b.Wait()
        0 // return an integer exit code
