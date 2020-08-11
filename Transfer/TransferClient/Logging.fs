// Learn more about F# at http://fsharp.org
namespace TransferClient
open System
open Logary
open Hopac
open Logary.Message
open Logary.Configuration
open Logary.Targets
open Logary.Targets.File
open Printf
module Logging=
   
    let logger = Log.create "Logary.HelloWorld"
    let fileConf =
            File.FileConf.create "./logs" (Naming ("{service}-{host}-{datetime}", "log")) 

    let logary =
        Config.create "Logary.ConsoleApp" "laptop"
        |> Config.targets[ 
            (File.create fileConf "file")
            (Targets.LiterateConsole.create LiterateConsole.empty "Console" )
            ]
        |> Config.ilogger (ILogger.Console Debug)
        |> Config.build
        |>run
    let initLogging()=
        logary|>ignore
    let infof fmt=
         ksprintf (logger.logSimple << Message.eventInfo )fmt
    let warnf fmt=
         ksprintf (logger.logSimple << Message.eventWarn )fmt
    let errorf fmt=
         ksprintf (logger.logSimple << Message.eventError )fmt
    let verbosef fmt=
        ksprintf (logger.logSimple << Message.eventVerbose )fmt
     
     
       

