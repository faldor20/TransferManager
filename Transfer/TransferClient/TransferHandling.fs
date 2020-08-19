namespace TransferClient
open SharedFs.SharedTypes
open IO.Types
open System.IO
open System
open DataBase.Types
open StackExchange.Profiling
module TransferHandling=


    let sucessfullCompleteAction transferData source=
        Logging.infof "{Successfull} finished cleanup of %s" source
        { (transferData) with Status=TransferStatus.Complete; Percentage=100.0; EndTime=DateTime.Now} 
    let FailedCompleteAction transferData source=
        Logging.warnf "{Failed} copying %s" source
        { (transferData) with Status=TransferStatus.Failed; EndTime=DateTime.Now} 
    let CancelledCompleteAction transferData source=
        Logging.infof "{Canceled} copying %s" source
        { (transferData) with Status=TransferStatus.Cancelled; EndTime=DateTime.Now}
    
    let processTask task =
        async{
        let transResult, transDataAccess, delete = Async.RunSynchronously task

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
                Logging.errorf" Could not delete file at after trying for a minute : %s " path
                return ()
            else
                try 
                    File.Delete(path) 
                with 
                    |_-> do! Async.Sleep(1000)
                         Logging.warnf "Couldn't delete file, probably in use somehow retrying"
                         do! del path (iterCount+1)
            }
        if delete then return! del source 0 
        else ()
        }
         
        
            
    