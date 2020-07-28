namespace TransferClient
open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open DataBase.Types
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

    type TransferDB=Dictionary<string,Dictionary<int,TransferData>>
    //this needs a dictionary for the index ebcuase someties two clients may share the same groupName meaning the id will not be sequential
    let mutable localDB: TransferDB=Dictionary()
    ///Database representing the changes made to the database since the last sync with the manager
    let mutable ChangeDB: TransferDB=Dictionary()
    //This is a list of changed parts of the local database this can be used to determine what should and shouldot be synced with the remote one
    let mutable ChangedEntries:((string*int)  ResizeArray)= ResizeArray()
    
    let getTransferData group index= localDB.[group].[index]
   
    let setTransferData  groupName index newData=
        lock ChangeDB (fun x->
        safeAdd2 ChangeDB groupName index newData
        )
        localDB.[groupName].[index]<- newData 

    let addTransferData  groupname newData=
        let index= Async.RunSynchronously<| ManagerCalls.addTransferData newData groupname
        lock localDB (fun x->
            lock ChangeDB (fun x->
                safeAdd2 ChangeDB groupname index newData
            )
            safeAdd2 localDB groupname index{newData with ID= 0}
        )
        index
    let AccessFuncs= {
        Set=setTransferData;
        Get=getTransferData;
        Add=addTransferData}