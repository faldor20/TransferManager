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

    let info  (message:string) (data:obj)=
        logger.Information(message, data)
    let info2  (message:string) (data:obj) (data2:obj)=
        logger.Information(message, data,data2)
    let info3  (message:string) (data:obj) (data2:obj) (data3:obj)=
        logger.Information(message, data,data2,data3)
                
    let error  (message:string) (data:obj)=
        logger.Error(message, data)
    let error2  (message:string) ( data:obj ) ( data2:obj )=
        logger.Error(message, data,data2)
    let error3  (message:string) (data:obj) ( data2:obj ) (data3:obj)=
        logger.Error(message, data,data2,data3)
       
    let warn  (message:string) (data:obj)=
        logger.Warning(message, data)
    let warn2  (message:string) (data:obj) (data2:obj)=
        logger.Warning(message, data,data2)
    let warn3  (message:string) (data:obj) (data2:obj) (data3:obj)=
        logger.Warning(message, data,data2,data3)
    
    let debug  (message:string) (data:obj)=
        logger.Debug(message, data)
    let debug2  (message:string) (data:obj) (data2:obj)=
        logger.Debug(message, data,data2)
    let debug3  (message:string) (data:obj) (data2:obj) (data3:obj)=
        logger.Debug(message, data,data2,data3)

