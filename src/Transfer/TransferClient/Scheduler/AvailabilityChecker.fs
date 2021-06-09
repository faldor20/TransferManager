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
type Status=
    |FinishLooping of Availability
    |ContinueLooping of FileInfo




let doAvailabilityCheck checker (fileName:string) (ct:CancellationToken) =
    if ct.IsCancellationRequested then
            FinishLooping Availability.Cancelled
    else        
        try
            let currentFile = new FileInfo(fileName)
            try
               match (checker currentFile) with
                    |Some x-> FinishLooping x
                    |None-> ContinueLooping currentFile
            with
                | :? IOException ->
                    ContinueLooping currentFile

                | ex  ->
                    Lgwarn2 "'Availability check' file {@file} failed with {@err}" fileName ex.Message
                    FinishLooping Availability.Deleted
        with 
            | :? FileNotFoundException | :? DirectoryNotFoundException  ->
                Lgwarn "'Availability check' {@file} deleted while waiting to be available" fileName
                FinishLooping Availability.Deleted
                
///Checks if a file is available by opening it and seeing if an error is triggered
let checkFileStream (lastFile:FileInfo)(currentFile:FileInfo) =
    Lgdebug "'Availability checker' {@file} being opened as stream" currentFile.Name 
    using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
        Lgdebug "'Availability checker' {@file} stream opened succesfully and now closing." currentFile.Name 
        stream.Close())
    Some Availability.Available

let checkFileWriteTime (lastFile:FileInfo) (currentFile:FileInfo)  =
        let newWriteTime=currentFile.LastWriteTime
        let oldWriteTime=lastFile.LastWriteTime
        Lgdebug3 "'Availability checker' {@file} oldTime={@old} newTime={@new}" currentFile.Name  oldWriteTime newWriteTime
        if newWriteTime=oldWriteTime then
            Lgdebugf "'Availability Checker' File is available. Returning"
            Some Availability.Available
        else None
    
///Checks if a file is bigger now thn last check
let checkFileSize (lastFile:FileInfo) (currentFile:FileInfo)  =
    let newSize=currentFile.Length
    let oldSize=lastFile.Length
    printfn "'Availability checker' {%s oldsize={%i} newSize={%i" currentFile.Name  oldSize newSize
    Lgdebug3 "'Availability checker' {@file} oldsize={@old} newSize={@new}" currentFile.Name  oldSize newSize
    if newSize=oldSize then
        Lgdebugf "'Availability Checker' File is available. Returning"
        Some Availability.Available
    else None
///Evaluates **f** if a is None. 
///Usefull for chainging failure states of options
let inline ifAvailable  f a=
    match a with
    |Availability.Available ->f()
    |a->Some a
///Evaluates **f** if a is None. 
///Usefull for chainging failure states of options
let inline ifNone f a=
    match a with
    |Some y->Some y
    |None->f()
let ifFinished f (a:Status)=
    match a with
    | FinishLooping b -> f b
    |_-> a
let checkSizeandWriteTime lastInfo currentInfo =
    checkFileSize lastInfo currentInfo
    |>Option.bind (ifAvailable (fun ()-> checkFileWriteTime lastInfo currentInfo))

    
///Checks if a file is available by applying the given checker at the given interval
let checkAvailability checker (source:string) (ct:CancellationToken) (checkInterval:int) =
    async {
        
        let fileName= Path.GetFileName (string source)
        let mutable loop = true
        let mutable availability=Availability.Available
        let mutable lastFile= new FileInfo(source)
        while loop do
            Lgdebug2 "'Availability checker' {@file} oldfile.Length={@length}" source lastFile.Length
            do! Async.Sleep(checkInterval)
            let inf= new FileInfo(source)
            printfn "len= %i"inf.Length
            let res=doAvailabilityCheck (checker lastFile) fileName ct
            match res with
            |FinishLooping av-> 
                availability<-av
                loop<-false
            |ContinueLooping currentFile ->
                lastFile<-currentFile
                ()
        return availability
    }

///This will return once the file is not being accessed by other programs.
///it returns false if the file is discovered to be deleted before that point.
let isAvailable (source:string) (ct:CancellationToken) (sleepTime:int option) =
    let sleepTime= sleepTime|>Option.defaultValue 1000
    checkAvailability checkSizeandWriteTime source ct sleepTime

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