namespace TransferClient

open Watcher
open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open FSharp.Control
open System.Collections.Generic
open TransferHandling
open IO.Types
module Manager =

    let startUp =
        async{
            //Read config file to get information about transfer source dest pairs
            let configData= ConfigReader.ReadFile "./WatchDirs.yaml"
            let mutable watchDirsData= configData.WatchDirs
            let groups=watchDirsData|>List.map(fun x-> x.MovementData.GroupList)
            //Create all the needed groups
            let mapping=configData.ScheduleIDMapping|>Seq.map(fun x-> KeyValuePair( x.Value,x.Key))|>Dictionary
            LocalDB.initDB groups configData.FreeTokens (processTask LocalDB.AcessFuncs) mapping
            //create a asyncstream that yields new schedule jobs when 
            //a new file is detected in a watched source
            let schedulesInWatchDirs = GetNewTransfers2  watchDirsData LocalDB.AcessFuncs
            
            
            let signalrCT=new Threading.CancellationTokenSource()
            //For reasons i entirely do not understand starting this just as async deosnt run connection in release mode
            Logging.infof "{Manager} starting signalr connection process"
            let conectionTask= SignalR.Commands.connect configData.manIP  configData.ClientName groups signalrCT.Token
            let jobs=schedulesInWatchDirs|>List.toArray|>Array.map(fun (schedules,grouList)->
                Logging.infof "{Manager}Setting up observables for group: %A" grouList
                schedules|>AsyncSeq.iterAsyncParallel(fun x-> x )
                )
            
           
            

            //Start the Syncing Service
            //TODO: only start this if signalr connects sucesfully
            //let res= jobs|>Observable.mergeArray|>Observable.subscribe(fun x->x|>Async.StartImmediate)
            let! conection=conectionTask
            ManagerSync.DBsyncer 500 conection configData.ClientName 

            let! a= jobs|>Async.Parallel|>Async.StartChild
            return!async{while true do do! Async.Sleep 100000 }
            0
        }


        
