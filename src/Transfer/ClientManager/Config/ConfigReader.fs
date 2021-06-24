module ClientManager.Config.Reader

open Thoth.Json.Net
open System.IO
open SharedFs.SharedTypes

let readConfig path =
    printfn "'Config' Reading config file at: %s" path

    let configText =
        try
            File.ReadAllText(path)
        with
        | :? IOException ->
            printfn "'Config' Could not find WatchDirs.yaml, that file must exist"
            "Failed To open 'WatchDirs.yaml' file must exist for program to run "


    let config =
        match Decode.Auto.fromString<UIConfig> (configText) with
        | Ok data -> data
        | Error err ->
            printfn "'Config'Config file (%s) malformed, there is an error: %s" configText err
            failwith "failed"

    match config.ColourScheme with
    | ColourScheme.Normal -> ()
    | ColourScheme.Alt -> ()
    | c -> printfn "ERROR:'ConfigReader' %A Not a supported colour scheme." c

    config
