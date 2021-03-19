mkdir '.\Publish\Transfer\ClientManager'
cd "./Transfer/ClientManager"
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --no-self-contained
robocopy "./bin/Release/net5.0/win-x64/publish" "../../Publish/Transfer/Clientmanager" /s 