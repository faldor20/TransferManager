namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
module DataBase=


    type TransferTaskDatabase=Dictionary<string,SortedDictionary<int,TransferData>>
    let mutable CancellationTokens=new Dictionary<string,CancellationTokenSource ResizeArray>()
    //this db is keyed 
    //groupName-> clientID->transferData index
    //group-> client ->TransferData
    let mutable dataBase=Dictionary<string,Dictionary<string, TransferData ResizeArray>>()

    
    
    let getTransferData group index= dataBase.[group].[index]

    let setTransferData newData group username index=
        //if the index is 0 it might be the first thing added so count will allso be 0
        if index=0 && dataBase.[group].[username].Count =0 then
            dataBase.[group].[username].Add(newData)  
               
        //index allready exists
        else if dataBase.[group].[username].Count > index then

            dataBase.[group].[username].[index] <- newData
        else 
            //Check if the count = index then we can just add and t will fill taht position
            if dataBase.[group].[username].Count = (index) 
                then dataBase.[group].[username].Add(newData)
            else printfn "ERROR: Index %i is equal to Count of DB and not 0. In user %s to group %s" index username group
   (*  let setTransferData2 newData group clientID index=
        
        if not(dataBase.[group].[clientID].Count > index) then

            if (dataBase.[group].[clientID].Count> (index+1)) 
            then dataBase.[group].[clientID].Add(newData)
            else printfn "ERROR: Adding index %i to user %s to group %s user database must't have given data sequetially" index clientID group
        else dataBase.[group].[clientID].[index]<- newData *)
    //this contains ->key:UserName Value: clientID
    let mutable userIDs= Dictionary<string,string>()

    (* let addTransferData newData key1=
      lock dataBase (fun  ()->   
            if dataBase.ContainsKey key1 then 
                
                //let Count=dataBase.[key1].Count
               //LOGGING: printfn "databaseCount for %s = %i" key1 Count
                dataBase.[key1].Add( {newData with ID=dataBase.[key1].Count })
               // let Count2=dataBase.[key1].Count
               //LOGGING: printfn "databaseCountAfter for %s = %i" key1 Count2
                ((dataBase.[key1].Count)-1)
            else 
                
                dataBase.Add(key1,new ResizeArray<TransferData>([{newData with ID= 0}])  )
                0
                 
            ) *)
    (* let mutable IDHolders= new Dictionary<string, string ResizeArray >() *)
    
    let registerClient userName clientID =
        userIDs.[userName]<-clientID
    let getClientID userName =
        userIDs.[userName]
        
    
(*
    let setNewTaskID  groupName dbId requester=
        let id= IDHolders.[groupName].Count
        IDHolders.[groupName].Add(requester)
        if dbId<> id then printfn "[ERROR] Something has gone very wrong, the main database and the connectionId database are out of sync "     
 *)

   (*  let addCancellationToken key token=
        lock CancellationTokens (fun()->
            if CancellationTokens.ContainsKey key then
                CancellationTokens.[key].Add(token)
            else 
                let res=CancellationTokens.TryAdd(key,(new ResizeArray<CancellationTokenSource>([token])))
                if not res then printfn"[ERROR]Something went wrong creating token list for %s " key
        ) *)

        