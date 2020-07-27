namespace ClientManager.Data
open System.Collections.Generic
open System.Collections.Specialized
open System;
open System.Threading
open System.IO
open SharedFs.SharedTypes;
open IOExtensions;
open ClientManager.Data.DataBase
module DataBaseSync=
    let internal syncIndexLevel groupName (changes:Dictionary<int,TransferData>)=
        let indexs= seq changes.Keys
        let transferDatas= seq changes.Values
        (indexs,transferDatas)||>Seq.iter2(fun index transData->
            setTransferData transData groupName index )

    let internal syncGrouplevel(changes:Dictionary<string, Dictionary<int,TransferData>>)=
        let groupNames= seq changes.Keys
        let transferDatas= seq changes.Values
        (groupNames,transferDatas)||>Seq.iter2(fun groupName changedData->syncIndexLevel groupName changedData )
   
    let syncDataBaseChanges (changes:Dictionary<string, Dictionary<int,TransferData>>)=
        syncGrouplevel changes