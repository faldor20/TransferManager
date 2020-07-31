cd "./Transfer/TransferClient"
dotnet publish -c Release --no-self-contained
robocopy "./bin/Release/netcoreapp3.1/win-x64/publish" "../../Publish/Transfer/TransferClient" /s 
pause