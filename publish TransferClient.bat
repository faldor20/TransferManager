cd "./Transfer/TransferClient"
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --no-self-contained
robocopy "./bin/Release/netcoreapp3.1/win-x64/publish" "../../Publish/Transfer/TransferClient" /s 
pause