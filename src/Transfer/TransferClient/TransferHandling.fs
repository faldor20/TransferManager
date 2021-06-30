namespace TransferClient
open SharedFs.SharedTypes
open Mover.Types
open System.IO
open System
open JobManager.Access
open DataBase.Types
open LoggingFsharp
module TransferHandling=
  
    
    let sucessfullCompleteAction transferData source=
        Lginfof "{Successfull} finished cleanup of %s" source
        { (transferData) with Status=TransferStatus.Complete; Percentage=100.0; EndTime=DateTime.Now} 
    let FailedCompleteAction transferData source=
        Lgwarnf "{Failed} copying %s" source
        { (transferData) with Status=TransferStatus.Failed; EndTime=DateTime.Now} 
    let CancelledCompleteAction transferData source=
        Lginfof "{Canceled} copying %s" source
        { (transferData) with Status=TransferStatus.Cancelled; EndTime=DateTime.Now}
    let cleaupTask (dbAcess:DBAccess)  jobID sourceID transResult delete=
        async{
            Lgdebug "'Transfer Handling' Finished Job {@job} Starting cleanup" jobID
            //TODO: This hangs for some reason?
            let transData= dbAcess.TransDataAccess.Get jobID
            let source = transData.Source
           //LOGGING: printfn "DB: %A" dataBase
            let dataChange=
                match transResult with 
                    |TransferResult.Success-> sucessfullCompleteAction transData source
                    |TransferResult.Cancelled-> CancelledCompleteAction transData source
                    |TransferResult.Failed-> FailedCompleteAction transData source
                    |_-> failwith "unknonw enum for transresult"
            
            dbAcess.TransDataAccess.SetAndSync jobID dataChange
            dbAcess.MakeJobFinished sourceID jobID
           
           
            let rec del path iterCount= async{
                if iterCount>10 
                then 
                    Lgerrorf" Could not delete file at after trying for a minute : %s " path
                    return ()
                else
                    try 
                        File.Delete(path) 
                    with 
                        | :?IOException-> 
                            do! Async.Sleep(1000)
                            Lgwarnf "Couldn't delete file, probably in use somehow retrying"
                            do! del path (iterCount+1)
                        |e->Lgerrorf "file deletion failed because of an unhandled reason %s\n Full exception: \n %A"e.Message e
                }
            if delete then
                match transData.TransferType with
                |local when local=TransferTypes.LocaltoLocal|| local=TransferTypes.LocaltoFTP->
                    return! del source 0 
                |ftp-> Lgwarnf "Deleting files after transfer that are only acessable via ftp is not currently supported. Pleases set that watchdir to not delete in config"
            else ()

        }
    let processTask (dbAcess:DBAccess) sourceID jobID =
        async{
            try
            let task= (dbAcess.JobListAccess.GetJob jobID).Value.Job
            let! transResult, delete =  task
            
            cleaupTask dbAcess jobID sourceID transResult delete |>Async.Start
            with|e->
                dbAcess.MakeJobFinished sourceID jobID
                let transdata=dbAcess.TransDataAccess.Get jobID
                dbAcess.TransDataAccess.SetAndSync jobID (FailedCompleteAction transdata transdata.Source )

                Lgerrorf"Exception throw while running job %i source: %i \n EX: %A"jobID sourceID e
        }
         
        
            
    