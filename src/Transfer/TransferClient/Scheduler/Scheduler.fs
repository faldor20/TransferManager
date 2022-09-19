
module TransferClient.Scheduling.Scheduler
open TransferClient.SignalR 
open System.IO
open System.Threading
open System
open Mover.Types
open SharedFs.SharedTypes
open TransferClient.DataBase.Types

open FluentFTP
open JobManager
open JobManager.Main
open FileWatcher.AvailabilityChecker
open TransferClient
open LoggingFsharp

let getFileData (file:FoundFile) currentTransferData=
    let fileSize=
        match file.FTPFileInfo with 
        |Some x->
           x.Size 
        |None ->
            (new FileInfo(file.Path)).Length
    let fileSizeMB=(float(fileSize/int64 1000))/1000.0

    {currentTransferData with FileSize=fileSizeMB}
///AvailabilityConfig must be some if availabilityTest contains a "SizeThreshold instance"
let mapAvailabilityTest (availabilityConfig:AvailabilityConfig option) (availabilityTest:AvailableTest list option)=
    let getFunc (av:AvailableTest)  =
        match av with
        |AvailableTest.LastSize->Checks.checkFileSize
        |AvailableTest.OpenAsStream->Checks.checkFileStream
        |AvailableTest.LastWriteTime->Checks.checkFileWriteTime
        |AvailableTest.SizeThreshold->Checks.checkFileSizeThreshold availabilityConfig.Value.ThresholdBytes 
        |_->raise (ArgumentException (sprintf"enum val %A is not supported" availabilityTest))

    availabilityTest
    |>Option.map(List.map getFunc)
    |>Option.defaultValue([Checks.checkFileSize;Checks.checkFileWriteTime;Checks.checkFileStream])
    |>checkList


let transferType moveData=
            let{SourceFTPData= source; DestFTPData=dest;}=moveData
            match (source,dest) with
            |Some _,Some _->TransferTypes.FTPtoFTP
            |None,Some _->TransferTypes.LocaltoFTP
            |Some _,None->TransferTypes.FTPtoLocal
            |None,None->TransferTypes.LocaltoLocal

let makeTransData moveData sourceFilePath file id:TransferData=
            getFileData 
                file
                { Percentage = 0.0
                  FileSize = 0.0
                  FileRemaining = 0.0
                  Speed = 0.0
                  Destination = moveData.DirData.DestinationDir
                  Source =  sourceFilePath
                  StartTime =  DateTime.Now
                  jobID = id
                  location=moveData.GroupList|>List.toArray
                  TransferType=transferType moveData 
                  Status = TransferStatus.Unavailable 
                  ScheduledTime=DateTime.Now
                  EndTime=new DateTime()}

let shouldTranscode moveData file=
    let extension= (Path.GetExtension file.Path)

    match moveData.TranscodeData with
    |Some x-> x.TranscodeExtensions|>List.contains extension
    |None-> false
let scheduleTransfer (file:FoundFile) (moveData:MovementData) (receiverFuncs:ReceiverFuncs option) (dbAccess:Access.DBAccess) =
    async {
        
        let transcode= shouldTranscode moveData file

        let sourceID=(List.last(moveData.GroupList))
        //this is only used for logging
        let logFilePath=match file.FTPFileInfo with | Some f-> "FTP:"+f.FullName |None -> file.Path

        let {DestinationDir=dest}:DirectoryData=moveData.DirData
        //TODO: make an event that is subscribed to this that cancells the job
        let ct = new CancellationTokenSource()

        //These two functions are passed into the AddJob function where they will be given a jobID
        //This is so the job can contain its own id.
        let makeTrans id=makeTransData moveData file.Path file id
        //See above
        let makeJob jobID=
            let moveJobData:MoveJobData=
                {SourcePath=file.Path
                 Transcode=transcode
                 CT=ct.Token
                 GetTransferData=(fun ()->dbAccess.TransDataAccess.Get jobID)
                 HandleTransferData=(fun changeFunc->dbAccess.TransDataAccess.UpdateAndSync jobID changeFunc)
                 ReceiverFuncs=receiverFuncs
                 }
            {Job=(Mover.Main.MoveFile moveData moveJobData); SourceID=sourceID; ID=jobID; Available=false; TakenTokens=List.Empty ;CancelToken=ct}

        let! jobID= dbAccess.AddJob (sourceID) makeJob makeTrans
        
        let setStatus status data =
               {data with Status=status} 
        let updateTransData data =
            dbAccess.TransDataAccess.UpdateAndSync jobID data
        //We register a function that will cancell the job if cancellation is requested while it is waiting to be started
        (* let waitingCancel=ct.Token.Register (Action (fun ()-> 
            let transdata=dbAccess.TransDataAccess.Get(jobID)
            if(transdata.Status=TransferStatus.Waiting) then
                Lginfo "'Scheduler' File cancelled while waiting to be available Transfer file at: {@file}" logFilePath 
                updateTransData( setStatus TransferStatus.Cancelled (transdata))|>Async.RunSynchronously
                TransferHandling.cleaupTask dbAccess jobID sourceID TransferResult.Cancelled moveData.DirData.DeleteCompleted
                    |>Async.RunSynchronously
                ct.Dispose()
            )) *)
        
        let transType=
            ""  |>fun s->if transcode then s+" transcode"else s
                |>fun s->if moveData.SourceFTPData.IsSome||moveData.DestFTPData.IsSome then s+" ftp" else s
        Lginfof "'Scheduled' %s transfer from %s To-> %s at index:%A" transType logFilePath dest jobID
        
        //This should only be run if reading from growing files is disabled otherwise ignore it.
        //Doesn't work on ftp files
        let! fileAvailable=
            match moveData.SourceFTPData with
                |Some ftpData-> 
                    let client=Mover.FTPMove.ftpClient ftpData
                    client.Connect()
                    file.Path|>isAvailableFTP  ct.Token client
                |None-> 
                    let checkFunc=mapAvailabilityTest moveData.DirData.AvailabilityConfig moveData.DirData.FileAvailableTest
                    isAvailable file.Path ct.Token moveData.SleepTime checkFunc
            
                
    
        if fileAvailable= Availability.Available then
            Lginfo "'Scheduler' file at: {@file} is available" logFilePath 
            do! setStatus TransferStatus.Waiting >>getFileData file |>updateTransData
            do! dbAccess.MakeJobAvailable jobID
            
            
        else if fileAvailable =Availability.Deleted then
            Lgwarn "'Scheduler' File Deleted  while waiting to be available Transfer file at: {@file}" logFilePath 
            do! setStatus TransferStatus.Failed  |>updateTransData
            do! TransferHandling.cleaupTask dbAccess jobID sourceID TransferResult.Failed moveData.DirData.DeleteCompleted
            
        else 
            Lginfo "'Scheduler' File cancelled while waiting to be available Transfer file at: {@file}" logFilePath 
            do! setStatus TransferStatus.Cancelled |>updateTransData
            do! TransferHandling.cleaupTask dbAccess jobID sourceID TransferResult.Cancelled moveData.DirData.DeleteCompleted
        
    }
