

module TransferClient.Scheduling.AvailabilityChecker 
open TransferClient
open System.IO
open System.Threading
open System
open TransferClient.IO.Types
open SharedFs.SharedTypes
open TransferClient.DataBase.Types
open TransferClient.IO
open FluentFTP
open TransferClient.JobManager
open TransferClient.JobManager.Main
type Availability=
    |Available=0
    |Deleted=1
    |Cancelled=2
///Checks if a file is aailable by opening it for exclusive use and waiting for an exception
//This will return once the file is not being acessed by other programs.
//it returns false if the file is discovered to be deleted before that point.
let checkAvailabilityFileStream (source:string) (ct:CancellationToken) =
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
                    Logging.warn "'Availability check' {@file} deleted while waiting to be available" fileName
                    availability<-Availability.Deleted
                    loop<-false
                | :? IOException ->
                    
                    do! Async.Sleep(1000)

                | ex  ->
                    Logging.warn2 "'Availability check' file {@file} failed with {@err}" fileName ex.Message
                    loop<- false
                    availability<-Availability.Deleted
        return availability
    }
///Checks if s file is available by checking if it's size is bigger than last time.
let checkAvailabilityFileSize (source:string) (ct:CancellationToken) (sleepTime:int)=
    async {
            let fileName= Path.GetFileName (string source)
            let mutable currentFile = new FileInfo(source)
            let mutable loop = true
            let mutable availability=Availability.Available
            let mutable lastSize= currentFile.Length
            while loop do
                do! Async.Sleep(sleepTime)
                if ct.IsCancellationRequested then
                    availability<- Availability.Cancelled
                    loop<- false
                try
                    let newSize=(new FileInfo(source)).Length
                    Logging.debug3 "'Availability checker' {@file} olsize={@old} newSize={@new}" fileName newSize lastSize
                    if newSize=lastSize then
                        availability<-Availability.Available
                        loop<-false
                        Logging.debugf "'Availability Checker' File is available. Returning"
                    lastSize<-newSize
                with 
                    | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                        Logging.warn "'Availability check' {@file} deleted while waiting to be available" fileName
                        availability<-Availability.Deleted
                        loop<-false
                    | :? IOException ->()

                    | ex  ->
                        Logging.warn2 "'Availability check' file {@file} failed with {@err}" fileName ex.Message
                        loop<- false
                        availability<-Availability.Deleted
            return availability
        }

///This will return once the file is not being accessed by other programs.
///it returns false if the file is discovered to be deleted before that point.
let isAvailable (source:string) (ct:CancellationToken) (sleepTime:int option) =
    let sleepTime= sleepTime|>Option.defaultValue 1000
    checkAvailabilityFileSize source ct sleepTime

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
                        if not (client.FileExists(source)) then failwith "" 
                        let! newModDate = Async.AwaitTask<|client.GetModifiedTimeAsync(source,token= ct)
                        if modifiedDate=  newModDate then
                            try
                                let read=client.OpenRead(source)
                                availability<-Availability.Available
                                loop<-false
                                read.Close()
                            with|_->()
                         else
                            modifiedDate<-newModDate
                            
                    with|ex->
                        Logging.error "'Scheduler' Error thrown during availability check {@exp}" ex
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