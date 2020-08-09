cd "./HostedBlazor/Server"
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true
robocopy "./bin/Release/netcoreapp3.1/win-x64/publish/" "../../ContainedPublish/WebServer/" /s 