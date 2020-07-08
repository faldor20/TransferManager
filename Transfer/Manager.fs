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
open FSharp.Control.Reactive
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
    
    let processTask groupName task=

        let transResult, id = Async.RunSynchronously task

        let transData=dataBase.[groupName].[id]
        let source = dataBase.[groupName].[id].Source

       //LOGGING: printfn "DB: %A" dataBase
       
        match transResult with 
            |TransferResult.Success-> sucessfullCompleteAction transData groupName id source
            |TransferResult.Cancelled-> CancelledCompleteAction transData groupName id source
            |TransferResult.Failed-> FailedCompleteAction transData groupName id source
            |_-> printfn "unknonw enum for transresult"
       
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
        async{
            do! del source 0  
            }
    

    let startUp =
        let mutable watchDirsData= ConfigReader.ReadFile "./WatchDirs.yaml"
                   
        let schedulesInWatchDirs = GetNewTransfers2  watchDirsData

        let resetWatch= 
            async{
                while true do
                    if DateTime.Now.TimeOfDay=TimeSpan.Zero then Data.reset()
                    do!Async.Sleep(1000*60)
        
            }
        Async.Start(resetWatch)
    
        printfn "%i schedule groups" schedulesInWatchDirs.Length

        let observables= schedulesInWatchDirs|>List.map(fun (schedules,groupName)->
            printfn "Setting up observables for group: %s" groupName
            let res=
                schedules
                    |>AsyncSeq.toObservable
                    |>Observable.bind Observable.ofAsync
                    |>Observable.iter(fun x->
                        Async.Start( processTask groupName x))
            res
            )
        let outPut=observables|>Observable.mergeSeq
        outPut|>Observable.wait
        
