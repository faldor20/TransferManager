namespace TransferClient
open System
open Serilog

open Microsoft.Extensions.Configuration
open Serilog.Sinks
open Serilog.Configuration
open Printf

module Logging=
    let logger =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme=Sinks.SystemConsole.Themes.SystemConsoleTheme.Literate)
            .WriteTo.File("./logs/log-.log" ,Serilog.Events.LogEventLevel.Verbose)
            .CreateLogger();


    let initLogging()=
        logger|>ignore
    let infof fmt=
        ksprintf (logger.Information )fmt
    let warnf fmt=
        ksprintf (logger.Warning )fmt
    let errorf fmt=
        ksprintf (logger.Error )fmt
    let verbosef fmt=
        ksprintf (logger.Verbose )fmt
    let debugf fmt=
        ksprintf (logger.Debug )fmt
     
     
       

