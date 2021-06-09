module Availability
open TransferClient.Scheduling.AvailabilityChecker

open System
open System.Runtime
open System.IO
open Expecto
open System.Threading
open FSharp.Control.Tasks.V2

let runintestEnv fn=
    let a=Directory.CreateDirectory ("./testDir")
    Directory.SetCurrentDirectory("./testDir")
    try
        fn()
    finally
        Async.RunSynchronously (Async.Sleep 100) //we have a delay to make sure all files are released and   with
        Directory.SetCurrentDirectory("../")
        Directory.Delete( "./testDir",true)
///Writes to the stream for an amount of tie and then disposes it
let writeIntermittently (file:string) (interval:int) numtimes=
    task{
        use a = File.AppendText(file)
        printfn "about to start writing"
        for i in 0..numtimes do
            a.WriteLine "heyy"
            do! Async.Sleep(interval)
    }
    
let simplecheck name (time:int)=
    let mutable cont=true
    let mutable lastInfo=new FileInfo(name)
    while cont do
       Async.RunSynchronously <|Async.Sleep(time)
       let newInfo= new FileInfo(name)
       printfn "new=%i old = %i "newInfo.Length lastInfo.Length
       if newInfo.Length= lastInfo.Length then
            cont<-false
    
let testInterval checker writeInterval writes (checkInterval:int)  =
    let task=
        task{
            let name= "./testfile.test"
            let a=File.WriteAllText(name,"sart")
            let source= CancellationTokenSource()
            let ct= source.Token
            source.CancelAfter(writeInterval*writes+(checkInterval*3))
            let writer= (writeIntermittently name writeInterval writes)
            //let! checker= Async.StartAsTask <| checkAvailability checker name ct checkInterval
            simplecheck name checkInterval
            if ct.IsCancellationRequested then 
                failwith "Took to long and had to call cancelation on checking. this means the check never sees it is avaialable"
            if writer.IsCompleted then 
                writer.Dispose()
                return true
            else 
                writer.Wait()
                writer.Dispose()
                return false
        }
    task.Wait()
    task.Result
///Test the availability checker with an interval that is to short and will result in the checker thinking he write is complete 
let testIncorrectInterval checker  =
        testInterval checker 500 20 300
///Test the availability checker with the apropriate checking interval
let testCorrectInterval checker  =
        testInterval checker 500 20 2100

[<Tests>]
let test=
    testSequenced<| testList "Availability checker tests" [
        (*
        test "file size checker"{
            let writeInterval=100
            let writes= 20
            let mutable a=true;
            let checkInterval =250
            let name= "./testfile.test"

            let source= CancellationTokenSource()
            let ct= source.Token
            
            Thread.Sleep(20)
            while  do
                let lastInfo=new FileInfo(name)
                printfn "lastInfo length = %i" lastInfo.Length
                Thread.Sleep(checkInterval)
                lastInfo.Refresh()
                printfn " out.length= %i"lastInfo.Length
                    
            Expect.isTrue(writer.IsCompleted) "done"
        } *)
    (*     test "simple"{
            let writeInterval=100
            let writes= 20
            let mutable a=true;
            let checkInterval =250
            let name= "./testfile.test"
            let writer= writeIntermittently name writeInterval writes
            writer.Dispose()
            Expect.isTrue(true) "hi"
        } *)
        test "availability file size check correct interval"{
            let res=  (fun ()-> testCorrectInterval checkFileSize)
                
            Expect.isTrue(res()) "File was considered available after writer had finished writing"
        };
        test "availability file size check incorrect interval"{
            let res=  (fun ()-> testIncorrectInterval checkFileSize)
                
            Expect.isFalse(res()) "File was considered available during writing, when the checking interval was too small, as expected"
        }
        test "availability open Stream check correct interval"{
            let res=  (fun ()-> testCorrectInterval checkFileStream)
        
            Expect.isTrue(res()) "File was considered available after writer had finished writing"
        };
        test "availability open Stream size check incorrect interval"{
            let res=  (fun ()-> testIncorrectInterval checkFileStream)
        
            Expect.isTrue(res()) "File Stream check not effectedbyt to small interval"
        }
    ]

