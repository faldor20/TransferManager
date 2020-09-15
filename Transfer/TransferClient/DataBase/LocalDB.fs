namespace TransferClient.DataBase
open System.Collections.Generic

open SharedFs.SharedTypes
open System.Threading
open TransferClient.SignalR
open Types
open Microsoft.AspNetCore.SignalR.Client
open TransferClient.RecDict
open TransferClient
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
    type RecTransDB=RecDict<string,ResizeArray<TransferData>,ResizeArray<TransferData>>
    type RecTransChangeDB=RecDict<string,Dictionary<int,TransferData>,Dictionary<int,TransferData>>
    //this needs a dictionary for the index ebcuase someties two clients may share the same groupName meaning the id will not be sequential
    let mutable private localDB: TransferDB=Dictionary()
    let mutable private localDBRec: RecTransDB=End (new ResizeArray<TransferData>())
    let getlocalDB()=localDB
    ///Database representing the changes made to the database since the last sync with the manager
    let mutable ChangeDB= Dictionary<string,Dictionary<int, TransferData >>()
    let mutable ChangeDBRec:RecTransChangeDB=End (new Dictionary<int,TransferData>())
    //This is a list of changed parts of the local database this can be used to determine what should and shouldot be synced with the remote one
    let mutable private ChangedEntries:((string*int)  ResizeArray)= ResizeArray()
    let mutable localID=0
    let getTransferData group index= localDB.[group].[index]

    let private setTransferData  groupName index newData=
        lock ChangeDB (fun x->
        safeAdd2 ChangeDB groupName index newData
        )
        localDB.[groupName].[index]<- newData 
        
    ///Adds a new object to the database getting its index from the ClientManager
    ///Sets the TransferObject ID to the index it is inserted at.
    let private addTransferData  groupName newData=
        let mutable index=0
        lock localDB (fun ()->
            lock ChangeDB (fun ()->
                if not( localDB.ContainsKey groupName) then 
                    localDB.Add(groupName,ResizeArray())
                    Logging.debugf "Cancellation token DB doesn't contain group: %s adding it now" groupName
                index<-localDB.[groupName].Count
                Logging.verbosef "{DataBase} Adding transfer data %s current length of array is %i" newData.Source index
                //we Set the ID to be the index so the Transdata can allways be refenced back too
                let indexedData= {newData with ID=index}
                localDB.[groupName].Add(indexedData)
                safeAdd2 ChangeDB groupName index indexedData
                Logging.verbosef "{DataBase} Added transfer data %s new length of array is %i "newData.Source localDB.[groupName].Count 
            )
        )
        index
    
    let initDB groups=
        Logging.infof "{DataBase} Initialising DataBase"
        groups|>List.iter(fun groupName->localDB.[groupName]<- new List<TransferData>())    
    //===========Recursive funtions=====
    let reset()=
        Logging.infof "{DataBase} Resetting DataBase"
        localDB<- Dictionary()
        ChangeDB<-Dictionary()
    let private setRecTransferData  groupKeys index newData=
        lock ChangeDBRec (fun ()->
            match drillToData ChangeDBRec groupKeys  with
            |Ok dat->
                (match dat with |MiddleType mid->mid|EndType en->en).[index]<-newData
            |Error err-> failwithf "failed to set chagnedb with error %s" err
        )
        match drillToData localDBRec groupKeys  with
        |Ok dat->
            (match dat with |MiddleType mid->mid|EndType en->en).[index]<-newData

        |Error err-> failwith <|sprintf"failed with err:%s" err
    let initRecDB keys=
        localDBRec<- RecDict.empty keys (new ResizeArray<TransferData>()) (new ResizeArray<TransferData>())
    let addData (recDB:RecTransDB) keys data=
        let mutable index= -1 //if the index is ever negative one we know soemting wnet wrong here
        lock localDBRec (fun ()->
            lock ChangeDBRec (fun ()->
            let res1=
                (drillToSameData recDB false keys ) |> Result.bind(fun x -> 
                index<- x.Count
                x.Add(data)
                Ok ())
            let res2=
                (drillToSameData ChangeDBRec true keys ) |> Result.bind(fun x -> 
                 x.Add(index,data)
                 Ok())
            match (res1,res2) with
            |(Ok _,Ok _)-> ()
            |(Error a,Ok _) -> failwith <|sprintf"one errors 1: %s" a 
            |(Ok _, Error a) -> failwith <|sprintf"one errors 1: %s" a 
            |(Error a,Error b)-> failwith <|sprintf"two errors 1: %s 2: %s" a b

            ))
    let getTransferDataRec keys index=
        match(drillToSameData localDBRec false keys)with
        |Ok a-> a.[index]
        |Error err->failwith <|sprintf"failed with error: %s"err
    let AccessFuncs= {
        Set=setRecTransferData;
        Get=getTransferDataRec ;
        Add=addData localDBRec}
