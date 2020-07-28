namespace TransferClient
open SharedFs.SharedTypes
open IOExtensions
open System.IO
open System
open DataBase.Types
module TransferHandling=


    let sucessfullCompleteAction transferData source=
        printfn " successfully finished copying %s" source
        { (transferData) with Status=TransferStatus.Complete; Percentage=100.0; EndTime=DateTime.Now} 
    let FailedCompleteAction transferData source=
        printfn "failed copying %s" source
        { (transferData) with Status=TransferStatus.Failed; EndTime=DateTime.Now} 
    let CancelledCompleteAction transferData source=
        printfn "canceled copying %s" source
        { (transferData) with Status=TransferStatus.Cancelled; EndTime=DateTime.Now}
    
    let processTask task =

        let transResult, transDataAccess = Async.RunSynchronously task

        let transData=transDataAccess.Get()
        let source = transData.Source

       //LOGGING: printfn "DB: %A" dataBase
        let dataChange=
            match transResult with 
                |TransferResult.Success-> sucessfullCompleteAction transData source
                |TransferResult.Cancelled-> CancelledCompleteAction transData source
                |TransferResult.Failed-> FailedCompleteAction transData source
                |_-> failwith "unknonw enum for transresult"
        transDataAccess.Set dataChange
       
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
    