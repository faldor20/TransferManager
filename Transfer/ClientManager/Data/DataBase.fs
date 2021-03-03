namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open TransferClient.JobManager
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
        IP:string
    }
    let mutable userIDs= Dictionary<string,ClientData>()
    
    let registerClient userName clientID iP =
        userIDs.[userName]<-{ClientID=clientID;IP=iP}
    let getConnectionID userName =
        userIDs.[userName].ClientID
    let getClientIP userName =
        userIDs.[userName].IP


        