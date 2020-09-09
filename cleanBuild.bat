del "./publish"

echo "publish web"
Call "./publish web.bat"
echo "publish client"
Call "./publish TransferClient.bat"
echo "publish manager"
Call "./publish ClientManager.bat"