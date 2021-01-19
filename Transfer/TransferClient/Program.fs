// Learn more about F# at http://fsharp.org
namespace TransferClient
open System
open System.Threading.Tasks
open Manager

module Main=

    //let webServer= new Task (fun () ->run Server.Server.app)
        
    let exceptionHandler (excep:UnhandledExceptionEventArgs) =
        let ex=(excep.ExceptionObject :?> Exception)
        Logging.errorf  "unhandled exception: %s || %A"  ex.Message ex
        ()
    //let main2= new Task (fun()-> startUp)
    let logTest()=
        Logging.errorf "Error log"
        Logging.warnf "Warn log"
        Logging.infof "Info log"
        Logging.debugf "Debug log"
        Logging.verbosef "Verbose log"
    [<EntryPoint>]
    let main argv =
        logTest()
        Logging.infof("Latest change: refactor of much of the code and switched from locks to single threaded db access")
        AppDomain.CurrentDomain.UnhandledException.Add exceptionHandler
        
     //   printfn"Begining"
      //  Testing.test 1
       // Benchmark.benchmark()
     //   printfn "done"   
    
        let a=Task.Run(fun()-> Async.RunSynchronously startUp)
        a.Wait()    
 
      
        

        0