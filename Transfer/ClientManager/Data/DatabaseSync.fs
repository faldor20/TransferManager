namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
open ClientManager.Data.DataBase
module DataBaseSync=

    let internal syncIndexLevel userName groupName (changes:Dictionary<int,TransferData>)=
        if not(dataBase.ContainsKey groupName) then 
           dataBase.Add(groupName,new Dictionary<string,TransferData ResizeArray>()  ) 
          //clientId exists?
        if not(dataBase.[groupName].ContainsKey userName) then
            dataBase.[groupName].Add(userName,new ResizeArray<TransferData>()  )

        let indexs= seq changes.Keys
        let transferDatas= seq changes.Values
        (indexs,transferDatas)||>Seq.iter2(fun index transData->
            setTransferData transData groupName userName index )

    let internal syncGrouplevel userName (changes:Dictionary<string, Dictionary<int,TransferData>>)=
         
      
        let groupNames= seq changes.Keys
        let transferDatas= seq changes.Values
        (groupNames,transferDatas)||>Seq.iter2(fun groupName changedData->syncIndexLevel userName groupName changedData )
   
    let syncDataBaseChanges userName (changes:Dictionary<string, Dictionary<int,TransferData>>)=
        syncGrouplevel userName changes