namespace TransferClient
open SharedFs.SharedTypes
open IO.Types
open System.IO
open System
open JobManager.Main
open DataBase.Types
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
    let cleaupTask (dbAcess:Access)  jobID sourceID transResult delete=
        async{
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
                    Logging.errorf" Could not delete file at after trying for a minute : %s " path
                    return ()
                else
                    try 
                        
                        File.Delete(path) 
                    with 
                        | :?IOException-> 
                            do! Async.Sleep(1000)
                            Logging.warnf "Couldn't delete file, probably in use somehow retrying"
                            do! del path (iterCount+1)
                        |e->Logging.errorf "file deletion failed because of an unhandled reason %s\n Full exception: \n %A"e.Message e
                }
            if delete then
                match transData.TransferType with
                |local when local=TransferTypes.LocaltoLocal|| local=TransferTypes.LocaltoFTP->
                    return! del source 0 
                |ftp-> Logging.warnf "Deleting files after transfer that are only acessable via ftp is not currently supported. Pleases set that watchdir to not delete in config"
            else ()
        }
    let processTask (dbAcess:Access) sourceID jobID =
        async{
            try
            let task= (dbAcess.GetJob jobID).Job
            let transResult, delete = Async.RunSynchronously task
            cleaupTask dbAcess jobID sourceID transResult delete |>Async.Start
            with|e->
                dbAcess.MakeJobFinished sourceID jobID
                let transdata=dbAcess.TransDataAccess.Get jobID
                dbAcess.TransDataAccess.SetAndSync jobID (FailedCompleteAction transdata transdata.Source )

                Logging.errorf"Exception throw while running job %i source: %i \n EX: %A"jobID sourceID e
        }
         
        
            
    