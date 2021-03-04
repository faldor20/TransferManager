namespace TransferClient
open Serilog
open Printf
open System

module Logging=
    let logger =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme=Sinks.SystemConsole.Themes.SystemConsoleTheme.Literate)
            .WriteTo.File("./logs/log-.log" ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .WriteTo.File("./logs/simpleLog-.log",Serilog.Events.LogEventLevel.Information,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .CreateLogger();
    let signalrLogger =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("./logs/SignalrLog-.log" ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
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
     
     
       

