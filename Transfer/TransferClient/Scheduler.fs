namespace TransferClient

open System.IO
open System.Threading
open System
open TransferClient.DataBase.TokenDatabase
open TransferClient.IO.Types
open SharedFs.SharedTypes
open DataBase.Types
open TransferClient.IO
open FluentFTP
module Scheduler =
    type Availability=
    |Available=0
    |Deleted=1
    |Cancelled=2
    //This will return once the file is not beig acessed by other programs.
    //it returns false if the file is discovered to be deleted before that point.
    let isAvailable (source:string) (ct:CancellationToken) =
        async {
            let fileName= Path.GetFileName (string source)
            let mutable currentFile = new FileInfo(source)
            let mutable loop = true
            let mutable availability=Availability.Available
            while loop do
                if ct.IsCancellationRequested then
                    availability<- Availability.Cancelled
                    loop<- false
                try
                    using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                        stream.Close())
                    availability<-Availability.Available
                    loop<-false
                with 
                    | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                        printfn "%s deleted while waiting to be available" fileName
                        availability<-Availability.Deleted
                        loop<-false
                    | :? IOException ->
                        
                        do! Async.Sleep(1000)

                    | ex  ->
                        printfn "file failed with %A" ex.Message
                        loop<- false
                        availability<-Availability.Deleted
            return availability
        }
    let isAvailableFTP (ct:CancellationToken) (client:FtpClient)(source:string)  =
            async {
                
                let mutable loop = true
                let mutable availability=Availability.Available
                let name=Path.GetFileName(source)
                let directory=Path.GetDirectoryName(source)
                let mutable modifiedDate=client.GetModifiedTime (source)
                while loop do
                    do! Async.Sleep 1000
                    if ct.IsCancellationRequested then
                        availability<- Availability.Cancelled
                        loop<- false
                    else
                        try 
                            let! newModDate = Async.AwaitTask<|client.GetModifiedTimeAsync(source,token= ct)
                            if modifiedDate=  newModDate then
                                availability<-Availability.Available
                                loop<-false
                             else
                                modifiedDate<-newModDate
                                
                        with|ex->
                            Logging.errorf "{Scheduler} Error thrown during availability check %A" ex
                            loop<-false
                            availability<-Availability.Deleted

                return availability
            }
        //i think this has some kind of overflow 
    let isAvailable2 source (ct:CancellationToken)=
        async{
            let rec loop (currentFile:FileInfo)  = 
                async {
                    do! Async.Sleep(1000)
                    try
                        using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                            stream.Close())
                        return Availability.Available
                    with 
                        | :? FileNotFoundException->
                             return Availability.Deleted
                        | :? IOException ->
                            if ct.IsCancellationRequested then
                                return Availability.Cancelled
                            else
                                return! loop(currentFile)
                        |_->failwith "something went very wrong while waiting for a file to become available"
                            return Availability.Deleted
                }
            return! loop(FileInfo(source))
        }
    let getFileData (file:FoundFile) currentTransferData=
        let fileSize=
            match file.FTPFileInfo with 
            |Some x->
               x.Size 
            |None ->
                (new FileInfo(file.Path)).Length
        let fileSizeMB=(float(fileSize/int64 1000))/1000.0

        {currentTransferData with
                FileSize=fileSizeMB
        }
     
    let scheduleTransfer (file:FoundFile) moveData (dbAccess:JobManager.JobDBAccess) transcode =
        async {
            //this is only used for logging
            let logFilePath=match file.FTPFileInfo with | Some f-> "FTP:"+f.FullName |None -> file.Path
            let {DestinationDir=dest}:DirectoryData=moveData.DirData
            let transferType=
                let{SourceFTPData= source; DestFTPData=dest;}=moveData
                match (source,dest) with
                |Some _,Some _->TransferTypes.FTPtoFTP
                |None,Some _->TransferTypes.LocaltoFTP
                |Some _,None->TransferTypes.FTPtoLocal
                |None,None->TransferTypes.LocaltoLocal
            let transData:TransferData=
                { Percentage = 0.0
                  FileSize = 0.0
                  FileRemaining = 0.0
                  Speed = 0.0
                  Destination = dest
                  Source =  file.Path
                  StartTime =  DateTime.Now
                  jobID = -1
                  location=moveData.GroupList
                  TransferType=transferType 
                  Status = TransferStatus.Unavailable 
                  ScheduledTime=DateTime.Now
                  EndTime=new DateTime()}
            //TODO: make an event that is subscribed to this that cancells the job
            let ct = new CancellationTokenSource()
            let jobID= dbAccess.JobList.AddJob  (fun id->{Job=Mover.MoveFile file.Path moveData  dbAccess id transcode ct;TakenTokens=List.Empty})
            dbAccess.TransferDataList.Set jobID transData
            addCancellationToken jobID ct
            //We register a function that will cancell the job if cancellation is requested while it is waiting to be started
            ct.Token.Register (Action (fun ()-> 
                if(dbAccess.TransferDataList.Get(jobID).Status=TransferStatus.Waiting) then
                    Logging.infof "{Scheduler} File cancelled while waiting to be available Transfer file at: %s" logFilePath 
                    dbAccess.TransferDataList.Set jobID {dbAccess.TransferDataList.Get jobID with Status=TransferStatus.Cancelled} 
                    TransferHandling.cleaupTask dbAccess jobID moveData.GroupList TransferResult.Cancelled moveData.DirData.DeleteCompleted
                        |>Async.RunSynchronously
                    ct.Dispose()
                ))|>ignore
            
            
            let transType=
                ""  |>fun s->if transcode then s+" transcode"else s
                    |>fun s->if moveData.SourceFTPData.IsSome||moveData.DestFTPData.IsSome then s+" ftp" else s
            Logging.infof "{Scheduled} %s transfer from %s To-> %s at index:%A" transType logFilePath dest jobID

            //This should only be run if reading from growing files is disabled otherwise ignroe it.
            //Doesn't work on ftp files
            let fileAvailable=
                match moveData.SourceFTPData with
                    |Some ftpData-> 
                        let client=IO.FTPMove.ftpClient ftpData
                        client.Connect()
                        Async.RunSynchronously<| (file.Path|>isAvailableFTP  ct.Token client)
                    |None-> Async.RunSynchronously( isAvailable file.Path ct.Token)
        

            if fileAvailable= Availability.Available then
                Logging.infof "{Available} file at: %s is available" logFilePath 
                dbAccess.TransferDataList.Set jobID (getFileData file ({dbAccess.TransferDataList.Get jobID with Status=TransferStatus.Waiting}) )  
                
            else if fileAvailable =Availability.Deleted then
                Logging.warnf "{Scheduler} File Deleted  while waiting to be available Transfer file at: %s" logFilePath 
                dbAccess.TransferDataList.Set jobID {dbAccess.TransferDataList.Get jobID with Status=TransferStatus.Failed} 
                do! TransferHandling.cleaupTask dbAccess jobID moveData.GroupList TransferResult.Failed moveData.DirData.DeleteCompleted
                
            else 
                Logging.infof "{Scheduler} File cancelled while waiting to be available Transfer file at: %s" logFilePath 
                dbAccess.TransferDataList.Set jobID {dbAccess.TransferDataList.Get jobID with Status=TransferStatus.Cancelled} 
                do! TransferHandling.cleaupTask dbAccess jobID moveData.GroupList TransferResult.Cancelled moveData.DirData.DeleteCompleted
        }
