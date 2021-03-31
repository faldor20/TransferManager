mkdir ..\..\Publish-linux\WebServer
cd "../../WebUI/Server"
dotnet publish -r linux-x64 -c Release /P:PublishSingleFile=true /P:UseAppHost=true
robocopy "./bin/Release/net5.0/linux-x64/publish/" "../../Publish-linux/WebServer/" /s 
pause