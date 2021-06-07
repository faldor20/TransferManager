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


let buildProject2 pathext source target outputPath selfContained   =
    let output= outputPath+pathext
    [output]|> Shell.cleanDirs
    source|>buildProject target output selfContained

let managerProj="src/Transfer/ClientManager/ClientManager.fsproj"
let clientProj="src/Transfer/TransferClient/TransferClient.fsproj"
let webUIProj="src/WebUI/Server/WebUI.Server.csproj"

let buildManager =
     buildProject2 "/Transfer/ClientManager" managerProj

let buildClient =
     buildProject2 "/Transfer/TransferClient" clientProj

let buildWebUI =
     buildProject2 "/WebServer" webUIProj

let buildProjects target outputPath selfContained =
    buildManager target outputPath selfContained
    buildClient target outputPath selfContained
    buildWebUI target outputPath true

Target.create "PubLinux" (fun _ -> buildProjects "linux-x64" "./Publish-linux" true)
Target.create "PubMan-Lin" (fun _ -> buildManager "linux-x64" "./Publish-linux" true )
Target.create "PubClient-Lin" (fun _ -> buildClient "linux-x64" "./Publish-linux" true )
Target.create "PubWeb-Lin" (fun _ -> buildWebUI "linux-x64" "./Publish-linux" true )

Target.create "PubWin" (fun _ -> buildProjects "win-x64" "./Publish-Win" false)


Target.create "All" ignore

"Clean" ==> "Build" ==> "All"

Target.runOrDefault "All"
