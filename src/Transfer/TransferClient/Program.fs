// Learn more about F# at http://fsharp.org
namespace TransferClient
open System
open System.Threading.Tasks
open Manager
open LoggingFsharp
module Main=

    //let webServer= new Task (fun () ->run Server.Server.app)
        
    let exceptionHandler (excep:UnhandledExceptionEventArgs) =
        let ex=(excep.ExceptionObject :?> Exception)
        Lgerrorf  "unhandled exception: %s || %A"  ex.Message ex
        ()
    //let main2= new Task (fun()-> startUp)
    let logTest()=
        Lgerrorf "Error log"
        Lgwarnf "Warn log"
        Lginfof "Info log"
        Lgdebugf "Debug log"
        Lgverbosef "Verbose log"
    [<EntryPoint>]
    let main argv =
        printfn "initialising logging"
        Logging.initLogging()
        printfn("logging initialised")
        logTest()
        Lginfof("Latest change: Switch to file size based availability checks")
        AppDomain.CurrentDomain.UnhandledException.Add exceptionHandler
        
     //   printfn"Begining"
      //  Testing.test 1
       // Benchmark.benchmark()
     //   printfn "done"   
    
        let a=Task.Run(fun()-> Async.RunSynchronously startUp)
        a.Wait()    
 
      
        

        0