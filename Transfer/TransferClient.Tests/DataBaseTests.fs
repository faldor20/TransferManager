module DataBaseTests

open TransferClient
open System
open SharedFs.SharedTypes
open Expecto
open Utils

let localDBAcess = DataBase.LocalDB.AccessFuncs
let sampleData: TransferData =
        { Destination = dest
          Source = source
          FileRemaining=10.0
          ID=0
          FileSize=10.0
          EndTime=DateTime.Now
          GroupName="hi"
          Speed = 0.0
          StartTime = System.DateTime.Now
          ScheduledTime = DateTime.Now 
          Status=TransferStatus.Waiting
          Percentage=0.0
          TransferType=TransferTypes.LocaltoLocal
          }
(* 
let setupDB() =
   DataBase.LocalDB.addTransferData "hi" sampleData
let resetDB() =
   DataBase.LocalDB.localDB<- System.Collections.Generic.Dictionary()

[<Tests>]
let DataBaseTests=
   testList "Data Base Tests" [
    test "Removing"{
        setupDB()|>ignore
        DataBase.LocalDB.localDB.Count|>Expect.isGreaterThan <| 0  <|"localDB not empty"
        SignalR.ClientApi.ResetDB.Invoke()
        Expect.isEmpty DataBase.LocalDB.localDB <|"localDBEmpty"
    }
    ]
 *)