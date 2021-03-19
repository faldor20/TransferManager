cd ./HostedBlazor/Server/;
dotnet publish -r win-x64 -c Release ;
cp -r "./bin/Release/net5.0/win-x64/publish/" "../../Publish/WebServer/" ;
