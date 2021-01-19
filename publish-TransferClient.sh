cd "./Transfer/TransferClient";
dotnet publish -r win-x64 -c Release --self-contained false ;
cp -r "./bin/Release/net5.0/win-x64/publish/" "../../Publish/Transfer/TransferClient" ; 
