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

    let mutable userIDs= Dictionary<string,string>()
    
    let registerClient userName clientID =
        userIDs.[userName]<-clientID
    let getClientID userName =
        userIDs.[userName]


        