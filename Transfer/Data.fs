namespace Transfer
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
open IOExtensions;
module Data=

    type ScheduledTransfer=Async<Async<TransferResult*int*CancellationToken>>
   
    type WatchDir =
        { 
          GroupName:string
          Dir: DirectoryInfo
          OutPutDir: string
          TransferedList: string list
          IsFTP:bool;
          ScheduledTasks:ScheduledTransfer list}
    //the simple watchDir is just a represntation of the exact object in the config file. It is used in deserialisation.
    type WatchDirSimple = { GroupName:string; Source: string; Destination: string; IsFTP:bool; }
     
   
      
     
     
     
   
    type TransferTaskDatabase=Dictionary<string,SortedDictionary<int,TransferData>>
    let nexId=ref 0
    let mutable CancellationTokens=new Dictionary<string,CancellationTokenSource ResizeArray>()
    let mutable dataBase=Map.empty<string,TransferData ResizeArray>
    let getTransferData ()= dataBase
    let setTransferData newData key1 index=
        if dataBase.ContainsKey key1 then 
            if dataBase.[key1].Count > index then
                dataBase.[key1].[index]<- newData
            else
                printfn "ERROR: Adding index %i to group %s of length %i the addtransfer data method should have added this already" index key1 dataBase.[key1].Count
                dataBase.[key1].Add(newData)
        else 
            dataBase<- dataBase.Add(key1,new ResizeArray<TransferData>([newData])  )
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
                
                dataBase<- dataBase.Add(key1,new ResizeArray<TransferData>([{newData with ID= 0}])  )
                0
                 
            )
    let removeItem key=dataBase.Remove key 
    let toSeq d = d |> Seq.map (fun (KeyValue(k,v)) -> (k,v))
    let getAsSeq= dataBase|>toSeq
    let GetCount key=
       // lock(dataBase){
       if dataBase.ContainsKey key then
            dataBase.[key].Count
            
        else 0
        //}
    let addCancellationToken key token=
        if CancellationTokens.ContainsKey key then
            CancellationTokens.[key].Add(token)
        else 
            CancellationTokens.Add(key,new ResizeArray<CancellationTokenSource>([token]))

    let reset ()=
        CancellationTokens<- new Dictionary<string,CancellationTokenSource ResizeArray>()
        dataBase<- Map.empty<string,TransferData ResizeArray>