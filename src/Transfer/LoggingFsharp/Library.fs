module LoggingFsharp
open Serilog
open Microsoft.FSharp.Core.Printf

let Lginfof fmt=
    ksprintf (Log.Information )fmt
let Lgwarnf fmt=
    ksprintf (Log.Warning )fmt
let Lgerrorf fmt=
    ksprintf (Log.Error )fmt
let Lgverbosef fmt=
    ksprintf (Log.Verbose )fmt
let Lgdebugf fmt=
    ksprintf (Log.Debug )fmt 

let Lginfo  (message:string) (data:obj)=
    Log.Information(message, data)
let Lginfo2  (message:string) (data:obj) (data2:obj)=
    Log.Information(message, data,data2)
let Lginfo3  (message:string) (data:obj) (data2:obj) (data3:obj)=
    Log.Information(message, data,data2,data3)
            
let Lgerror  (message:string) (data:obj)=
    Log.Error(message, data)
let Lgerror2  (message:string) ( data:obj ) ( data2:obj )=
    Log.Error(message, data,data2)
let Lgerror3  (message:string) (data:obj) ( data2:obj ) (data3:obj)=
    Log.Error(message, data,data2,data3)
   
let Lgwarn  (message:string) (data:obj)=
    Log.Warning(message, data)
let Lgwarn2  (message:string) (data:obj) (data2:obj)=
    Log.Warning(message, data,data2)
let Lgwarn3  (message:string) (data:obj) (data2:obj) (data3:obj)=
    Log.Warning(message, data,data2,data3)

let Lgdebug  (message:string) (data:obj)=
    Log.Debug(message, data)
let Lgdebug2  (message:string) (data:obj) (data2:obj)=
    Log.Debug(message, data,data2)
let Lgdebug3  (message:string) (data:obj) (data2:obj) (data3:obj)=
    Log.Debug(message, data,data2,data3)
