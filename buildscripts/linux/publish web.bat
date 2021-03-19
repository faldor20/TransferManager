mkdir ..\..\Publish-linux\WebServer
cd "../../HostedBlazor/Server"
dotnet publish -r linux-x64 -c Release 
robocopy "./bin/Release/net5.0/linux-x64/publish/" "../../Publish-linux/WebServer/" /s 
pause