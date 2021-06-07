module TransferClient.Scheduling.AvailabilityChecker 
open TransferClient
open System.IO
open System.Threading
open System
open Mover.Types
open SharedFs.SharedTypes
open TransferClient.DataBase.Types

open FluentFTP
open JobManager
open JobManager.Main
open LoggingFsharp
type Availability=
    |Available=0
    |Deleted=1
    |Cancelled=2

let doAvailabilityCheck checker (fileName:string) (ct:CancellationToken) =
    if ct.IsCancellationRequested then
            Some Availability.Cancelled
    else        
        try
            checker()
        with 
            | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                Lgwarn "'Availability check' {@file} deleted while waiting to be available" fileName
                Some Availability.Deleted

            | :? IOException ->
                None

            | ex  ->
                Lgwarn2 "'Availability check' file {@file} failed with {@err}" fileName ex.Message
                Some Availability.Deleted
                
///Checks if a file is available by opening it and seeing if an error is triggered
let FileStreamCheck (currentFile:FileInfo) ct=
    let checker ()=
        using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
            stream.Close())
        Some Availability.Available
    doAvailabilityCheck checker currentFile.FullName ct
///Checks if a file is bigger now thn last check
let FileSizeCheck (lastFile:FileInfo) (currentFile:FileInfo) ct =
    let newSize=currentFile.Length
    let oldSize=lastFile.Length
    let checker()=
        Lgdebug3 "'Availability checker' {@file} oldsize={@old} newSize={@new}" currentFile.Name  oldSize newSize
        if newSize=oldSize then
            Lgdebugf "'Availability Checker' File is available. Returning"
            Some Availability.Available
        else None
    doAvailabilityCheck checker currentFile.FullName ct
///Evaluates **f** if a is None. 
///Usefull for chainging failure states of options
let inline ifNone  f a=
    match a with
    |Some y->Some y
    |None->f()

let fileSizeAndStreamCheck lastFile currentFile ct=
    FileSizeCheck lastFile currentFile ct 
    |> ifNone (fun ()-> FileStreamCheck currentFile ct)
    
///Checks if a file is aailable by opening it for exclusive use and waiting for an exception
//This will return once the file is not being acessed by other programs.
//it returns false if the file is discovered to be deleted before that point.
let checkAvailability checker (source:string) (ct:CancellationToken) (sleepTime:int) =
    async {
        
        let fileName= Path.GetFileName (string source)
        let mutable loop = true
        let mutable availability=Availability.Available
        let mutable lastFile= new FileInfo(source)
        while loop do
            do! Async.Sleep(sleepTime)
            let currentFile = new FileInfo(source)
            match checker lastFile currentFile ct with
            |Some av-> 
                availability<-av
                loop<-false
            |None ->
                ()
            lastFile<-currentFile
        return availability
    }

///This will return once the file is not being accessed by other programs.
///it returns false if the file is discovered to be deleted before that point.
let isAvailable (source:string) (ct:CancellationToken) (sleepTime:int option) =
    let sleepTime= sleepTime|>Option.defaultValue 1000
    checkAvailability fileSizeAndStreamCheck source ct sleepTime

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
                        Lgerror "'Scheduler' Error thrown during availability check {@exp}" ex
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