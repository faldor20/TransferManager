namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
module DataBase=


    type TransferTaskDatabase=Dictionary<string,SortedDictionary<int,TransferData>>
    
    //key: clientname value: that clinets UIData 
    let mutable dataBase=Dictionary<string,UIData>()
    
    let getTransferData group id= dataBase.[group].TransferDataList.[id]

    let setTransferData newData username  id=
        //if the index is 0 it might be the first thing added so count will allso be 0
        dataBase.[username].TransferDataList.[id]<-newData
    type ClientData={
        ClientID:string
    }
    let mutable userIDs= Dictionary<string,ClientData>()
    
    let registerClient userName clientID =
        userIDs.[userName]<-{ClientID=clientID}
    let getConnectionID userName =
        try 
            Ok(userIDs.[userName].ClientID)
        with| ex-> 
            printfn "Error: could not find the client %s" userName
            Error(())
    ///This is very inneficient and should not be used often
    let tryGetUserName id =
        userIDs
        |>Seq.tryFind(fun pair->pair.Value.ClientID=id) 
        |>Option.bind(fun x->Some x.Value.ClientID)
            
   


        