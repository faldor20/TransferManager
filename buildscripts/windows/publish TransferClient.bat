mkdir .\Publish\Transfer\TransferClient
cd "./Transfer/TransferClient"
dotnet publish -r win-x64 -c Release --no-self-contained
robocopy "./bin/Release/net5.0/win-x64/publish" "../../Publish/Transfer/TransferClient" /s 
pause