mkdir -p ./Publish/Transfer/TransferClient
cd "./Transfer/ClientManager";
dotnet publish -r win-x64 -c Release --self-contained false ;
cp -r "./bin/Release/net5.0/win-x64/publish/." "../../Publish/Transfer/Clientmanager";