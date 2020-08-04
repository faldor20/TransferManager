namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
module DataBase=


    type TransferTaskDatabase=Dictionary<string,SortedDictionary<int,TransferData>>
    //this db is keyed 
    //groupName-> clientID->transferData index
    //group-> client ->TransferData
    let mutable dataBase=Dictionary<string,Dictionary<string,Dictionary<int, TransferData >>>()
    
    let getTransferData group index= dataBase.[group].[index]

    let setTransferData newData group username index=
        //if the index is 0 it might be the first thing added so count will allso be 0
        dataBase.[group].[username].[index]<-newData

    let mutable userIDs= Dictionary<string,string>()
    
    let registerClient userName clientID =
        userIDs.[userName]<-clientID
    let getClientID userName =
        userIDs.[userName]


        