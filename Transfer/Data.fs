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
   
   
    type TranscodeData=
        {
            TranscodeExtensions:string list;
            FfmpegArgs:string option;
        }
    let TranscodeData  transcodeExtensions ffmpegArgs = {TranscodeExtensions= transcodeExtensions; FfmpegArgs=ffmpegArgs}
    type FTPData={
        User:string
        Password:string
        Host:string
    }
    let FTPData  user password host= {User= user; Password=password; Host=host}
    type DirectoryData={
        GroupName:string
        SourceDir: string
        DestinationDir: string
    }
    let DirectoryData groupName source destination ={GroupName=groupName;SourceDir=source;DestinationDir=destination;}
    type MovementData={
        DirData:DirectoryData
        FTPData:FTPData option;
        TranscodeData:TranscodeData option;
    }
    type WatchDir =
        { 
          MovementData:MovementData
          TransferedList: string list
          ScheduledTasks:ScheduledTransfer list;
          }

      
    
   
    type TransferTaskDatabase=Dictionary<string,SortedDictionary<int,TransferData>>
    let nexId=ref 0
    let mutable CancellationTokens=new Dictionary<string,CancellationTokenSource ResizeArray>()
    let mutable dataBase=Map.empty<string,TransferData ResizeArray>
    let getTransferData group index= dataBase.[group].[index]
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
        lock CancellationTokens (fun()->
            if CancellationTokens.ContainsKey key then
                CancellationTokens.[key].Add(token)
            else 
                let res=CancellationTokens.TryAdd(key,(new ResizeArray<CancellationTokenSource>([token])))
                if not res then printfn"[ERROR]Something went wrong creating token list for %s " key
        )
    let reset ()=
        CancellationTokens<- new Dictionary<string,CancellationTokenSource ResizeArray>()
        dataBase<- Map.empty<string,TransferData ResizeArray>

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