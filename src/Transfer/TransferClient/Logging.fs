namespace TransferClient
open Serilog
open Printf
open System


module Logging=
    let startTime=DateTime.Now
    let logpath=sprintf "./logs/log%i;%i;%i-.log" startTime.Hour startTime.Minute startTime.Second
    printfn "%s"logpath 
    let logger =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme=Sinks.SystemConsole.Themes.SystemConsoleTheme.Literate)
            .WriteTo.File(logpath ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .WriteTo.File("./logs/simpleLog-.log",Serilog.Events.LogEventLevel.Information,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .CreateLogger();
    let signalrLogger =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("./logs/SignalrLog-.log" ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .CreateLogger();


    let initLogging()=
        logger|>ignore


