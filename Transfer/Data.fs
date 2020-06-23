namespace Transfer
open System.Collections.Generic
open System;
open SharedData;
module Data=
    type TransferData={
          Percentage:float 
          FileSize :float
          FileRemaining: float
          Speed:float
          Destination:string
          Source:string
          Status:TransferStatus
          StartTime:DateTime
          id:Guid 
        } 
    let mutable CancellationTokens= Dictionary<Guid,System.Threading.CancellationTokenSource>()
    let mutable data=Map.empty<System.Guid,TransferData>
    let getTransferData ()= data
    let setTransferData newData key=data<- data.Add (key,newData)
    let removeItem key=data.Remove key 
    let toSeq d = d |> Seq.map (fun (KeyValue(k,v)) -> (k,v))
    let getAsSeq= data|>toSeq
    