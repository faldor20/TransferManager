namespace Transfer

open System.IO
open System.Threading.Tasks
open Mover
open Watcher
open FSharp.Data
open FSharp.Json
open System
open IOExtensions
open Data;
open SharedFs.SharedTypes
open Legivel.Serialization
open FSharp.Control
module Manager =

  
    let sucessfullCompleteAction transferData groupName id source=
        printfn " successfully finished copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Complete; Percentage=100.0; EndTime=DateTime.Now} groupName id
    let FailedCompleteAction transferData groupName id source=
        printfn "failed copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Failed; EndTime=DateTime.Now} groupName id 
    let CancelledCompleteAction transferData groupName id source=
        printfn "canceled copying %A" source
        Data.setTransferData { (transferData) with Status=TransferStatus.Cancelled; EndTime=DateTime.Now} groupName id
    
    

    let startUp =
        let mutable watchDirsData= ConfigReader.ReadFile "./WatchDirs.yaml"
                   
        let schedules = GetNewTransfers2  watchDirsData

        let resetWatch= 
            async{
                while true do
                    if DateTime.Now.TimeOfDay=TimeSpan.Zero then Data.reset()
                    do!Async.Sleep(1000*60)
        
            }
        Async.Start(resetWatch)
        
        let completeTransferTasks = asyncSeq{ 
            for directoryGroup in schedules do
                //This schedules the tasks it needs to be paralell because all transfers should be scheduled immidiatley
                //as soon as the file is detected
                let schedules,groupName=directoryGroup
                let scheduledTasks=schedules|>AsyncSeq.mapAsyncParallel(fun scheduleTask-> scheduleTask)

                //This iterates though the transfer tasks. It is "iterAsync" and not parallel
                //becuase we want the transfers to be started one after another
                yield scheduledTasks|> AsyncSeq.iterAsync (fun task ->
                    async{
                        let! transResult, id,ct = task

                        let transData=dataBase.[groupName].[id]
                        let source = dataBase.[groupName].[id].Source

                       //LOGGING: printfn "DB: %A" dataBase
                       
                        match transResult with 
                            |TransferResult.Success-> sucessfullCompleteAction transData groupName id source
                            |TransferResult.Cancelled-> CancelledCompleteAction transData groupName id source
                            |TransferResult.Failed-> FailedCompleteAction transData groupName id source
                       
                        let rec del path iterCount= async{
                            if iterCount>10 
                            then 
                                printfn"Error: Could not delete file at after trying for a minute : %s " path
                                return ()
                            else
                                try 
                                    File.Delete(path) 
                                with 
                                    |_-> do! Async.Sleep(1000)
                                         printfn "Error Couldn't delete file, probably in use somehow"
                                         do! del path (iterCount+1)
                            }
                        do! del source 0
                        
                    }
                )}
        
        completeTransferTasks|>AsyncSeq.iterAsyncParallel(fun x ->x)
    
        
