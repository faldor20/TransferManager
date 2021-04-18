namespace TransferClient
open Serilog
open Printf
open System
open Serilog.Sinks.Grafana.Loki
open System.Collections.Generic
open System.IO
type LoggingConfig={
    ClientName:string
    LogPath:string option
    LokiURL:string
    labels:(string*string) list
}

type LokiInfo={
    URL:string;
    lables:IEnumerable<LokiLabel>

}

module Logging=
    let startTime=DateTime.Now
    let createLogger lokiInfo logPath =
        let logName=sprintf "%sdebugLog-%i-%i_%i;%i-%is--.log" logPath startTime.Month startTime.Day startTime.Hour startTime.Minute startTime.Second
        printfn "%s"logName
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme=Sinks.SystemConsole.Themes.SystemConsoleTheme.Literate)
            .WriteTo.File(logName ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .WriteTo.File(logPath+"simpleLog-.log",Serilog.Events.LogEventLevel.Information,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .WriteTo.GrafanaLoki(lokiInfo.URL,lokiInfo.lables,LokiLabelFiltrationMode.Include,[])
            .CreateLogger();
    let signalrLogger() =
        Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("./logs/SignalrLog-.log" ,Serilog.Events.LogEventLevel.Verbose,rollingInterval=RollingInterval.Day,fileSizeLimitBytes=(int64 (1000*1000)))
            .CreateLogger();

    let tryCatch f exHandler x =
        try
            Ok(f x) 
        with
        | ex -> (exHandler ex) |> Error 
      
    let getConfig = Thoth.Json.Net.Decode.Auto.fromString<LoggingConfig> 
    let makelabel (key,value) =
        let mutable label=new LokiLabel()
        label.Key<-key
        label.Value<- value
        label
    let makeLabels pairs =
        pairs|>List.map makelabel 
    let applyconfig (loggingConfig:LoggingConfig)=
        let nameLabel = makelabel ("ClientName" ,loggingConfig.ClientName)
        let labels=nameLabel::(makeLabels loggingConfig.labels)
        let logPath = loggingConfig.LogPath|>Option.defaultValue "./logs/"
        {URL=loggingConfig.LokiURL;lables=labels},logPath
    let initLogging lokiAddr=
        
        let text=  tryCatch (File.ReadAllText) (fun x->x.ToString()) "./logConfig.json"  
        let res=
            text 
            |> Result.bind getConfig
            |> Result.map applyconfig
        match res with 
        |Ok (lokiConfig,logPath)->  Log.Logger<-createLogger lokiConfig logPath
        |Error e->printfn"-\n-\n-\n-\nError reading logging config. Fix this or you will have no logging.\nThis is extremely bad\nError is: %s \n-\n-\n" e
        ()



