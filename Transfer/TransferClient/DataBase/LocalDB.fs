namespace TransferClient.DataBase
open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open Types
open Microsoft.AspNetCore.SignalR.Client
module LocalDB=

    let safeAdd1 (dict:Dictionary<'a,'b>)  key1 newData=
        if not(dict.ContainsKey key1) then
            dict.Add( key1,newData)
        else
            dict.[key1]<-newData

    let safeAdd2 (dict:Dictionary<'a,Dictionary<'b,'c>>) key1 key2 newData=
        
        if not (dict.ContainsKey key1) then
            dict.Add(key1, (new Dictionary<'b,'c>([KeyValuePair(key2,newData)])))
        else if not(dict.[key1].ContainsKey key2) then
            dict.[key1].Add(key2,newData)
        else
            dict.[key1].[key2]<-newData

    type TransferDB=Dictionary<string, TransferData ResizeArray>
    //this needs a dictionary for the index ebcuase someties two clients may share the same groupName meaning the id will not be sequential
    let mutable private localDB: TransferDB=Dictionary()
    let getlocalDB()=localDB
    ///Database representing the changes made to the database since the last sync with the manager
    let mutable ChangeDB= Dictionary<string,Dictionary<int, TransferData >>()
    //This is a list of changed parts of the local database this can be used to determine what should and shouldot be synced with the remote one
    let mutable private ChangedEntries:((string*int)  ResizeArray)= ResizeArray()
    let mutable localID=0
    let getTransferData group index= localDB.[group].[index]

    let private setTransferData  groupName index newData=
        lock ChangeDB (fun x->
        safeAdd2 ChangeDB groupName index newData
        )
        localDB.[groupName].[index]<- newData 
    let initDB groups=
        TransferClient.Logging.infof "{DataBase} Initialising DataBase"
        groups|>List.iter(fun groupName->localDB.[groupName]<- new List<TransferData>())
    ///Adds a new object to the database getting its index from the ClientManager
    ///Sets the TransferObject ID to the index it is inserted at.
    let private addTransferData  groupName newData=
        let mutable index=0
        lock localDB (fun ()->
            lock ChangeDB (fun ()->
                if not( localDB.ContainsKey groupName) then 
                    localDB.Add(groupName,ResizeArray())
                    TransferClient.Logging.debugf "Cancellation token DB doesn't contain group: %s adding it now" groupName
                index<-localDB.[groupName].Count
                TransferClient.Logging.verbosef "{DataBase} Adding transfer data %s current length of array is %i" newData.Source index
                //we Set the ID to be the index so the Transdata can allways be refenced back too
                let indexedData= {newData with ID=index}
                localDB.[groupName].Add(indexedData)
                safeAdd2 ChangeDB groupName index indexedData
                TransferClient.Logging.verbosef "{DataBase} Added transfer data %s new length of array is %i "newData.Source localDB.[groupName].Count 
            )
        )
        index
    let reset()=
        TransferClient.Logging.infof "{DataBase} Resetting DataBase"
        localDB<- Dictionary()
        ChangeDB<-Dictionary()
    let AccessFuncs= {
        Set=setTransferData;
        Get=getTransferData;
        Add=addTransferData}