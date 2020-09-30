cd "./HostedBlazor/Server"
dotnet publish -r win-x64 -c Release 
robocopy "./bin/Release/netcoreapp3.1/win-x64/publish/" "../../Publish/WebServer/" /s 
pause