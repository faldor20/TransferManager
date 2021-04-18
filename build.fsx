#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment ()

Target.create "Clean" (fun _ -> !! "src/**/bin" ++ "src/**/obj" |> Shell.cleanDirs)

Target.create "Build" (fun _ -> !! "src/**/*.*proj" |> Seq.iter (DotNet.build id))

let buildProject target output selfContained projectFile  =
    try
        printfn "trying to build project %s" projectFile
        projectFile
        |> DotNet.publish
            (fun options ->
                { options with
                      Configuration = DotNet.BuildConfiguration.Release
                      OutputPath = Some output
                      SelfContained = Some selfContained
                      Runtime = Some target })
    with ex -> printfn "failed to build %s: reason %A" projectFile ex

let buildProjects target outputPath selfContained =
    let managerOutput = outputPath + "Transfer/Clientmanager"
    let clientOutput = outputPath + "/Transfer/TransferClient"
    let webOutput = outputPath + "/WebServer"

    [ managerOutput
      clientOutput
      webOutput ]
    |> Shell.cleanDirs
    
    "src/Transfer/ClientManager/ClientManager.fsproj"
    |> buildProject target managerOutput selfContained

    "src/Transfer/TransferClient/TransferClient.fsproj"
    |> buildProject target clientOutput selfContained

    "src/WebUI/Server/WebUI.Server.csproj"
    |> buildProject target webOutput true

Target.create "PubLinux" (fun _ -> buildProjects "linux-x64" "./Publish-linux/" true)

Target.create "PubWin" (fun _ -> buildProjects "win-x64" "./Publish-Win/" false)


Target.create "All" ignore

"Clean" ==> "Build" ==> "All"

Target.runOrDefault "All"
