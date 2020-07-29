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
    let mutable dataBase=Dictionary<string,TransferData ResizeArray>()

    
    
    let getTransferData group index= dataBase.[group].[index]

    let setTransferData newData key1 index=
        if dataBase.ContainsKey key1 then 
            if dataBase.[key1].Count > index then
                dataBase.[key1].[index]<- newData
            else
                printfn "ERROR: Adding index %i to group %s of length %i the addtransfer data method should have added this already" index key1 dataBase.[key1].Count
                dataBase.[key1].Add(newData)
        else 
            dataBase.Add(key1,new ResizeArray<TransferData>([newData])  )

    let addTransferData newData key1=
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
                 
            )
    
    let mutable IDHolders= new Dictionary<string, string ResizeArray >()
    
    let registerClient groupName =
        let a= IDHolders.TryAdd(groupName,List<string>()) 
        let b=dataBase.TryAdd(groupName,List<TransferData>())
        if a&&b then 
            printfn "sucessfully registered group:%s" groupName
        else
            printfn "registered new client for group %s" groupName
        ()
    let getClient groupName id=
        IDHolders.[groupName].[id]

    let setNewTaskID  groupName dbId requester=
        let id= IDHolders.[groupName].Count
        IDHolders.[groupName].Add(requester)
        if dbId<> id then printfn "[ERROR] Something has gone very wrong, the main database and the connectionId database are out of sync "     


    let addCancellationToken key token=
        lock CancellationTokens (fun()->
            if CancellationTokens.ContainsKey key then
                CancellationTokens.[key].Add(token)
            else 
                let res=CancellationTokens.TryAdd(key,(new ResizeArray<CancellationTokenSource>([token])))
                if not res then printfn"[ERROR]Something went wrong creating token list for %s " key
        )
         
    let reset ()=
        CancellationTokens<- new Dictionary<string,CancellationTokenSource ResizeArray>()
        dataBase<- Dictionary<string,TransferData ResizeArray>()

    let resetWatch= 
        async{
            while true do
                let hour=DateTime.Now.Hour
                if hour=1 then
                    reset()
                    printfn "Reset list of jobs"
                printfn "waiting for hour 1 to reset currently hour= %i" hour   
                do!Async.Sleep(1000*60*59)

        }

        