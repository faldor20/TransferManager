mkdir '..\..\Publish-linux\Transfer\ClientManager'
cd "../../Transfer/ClientManager"
dotnet publish -r linux-x64 -c Release /p:PublishSingleFile=true 
robocopy "./bin/Release/net5.0/linux-x64/publish" "../../Publish-linux/Transfer/Clientmanager" /s 