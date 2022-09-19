module Tests

open Expecto

open System
open System.Runtime
open System.IO
open System.Threading
open FileWatcher.AvailabilityChecker
open FileWatcher.AvailabilityChecker.Checks

let runintestEnv fn=
    let dir=Path.GetFullPath(sprintf "./testDir%A" (Guid.NewGuid()))
    printfn "The testing dir is %A" dir
    let a=Directory.CreateDirectory (dir)
    Directory.SetCurrentDirectory(dir)
    try
        fn()
    finally
        Async.RunSynchronously (Async.Sleep 100) //we have a delay to make sure all files are released and   with
        Directory.SetCurrentDirectory("../")
        Directory.Delete( dir,true)
let doIntermittently(interval:int) numtimes res (fn:unit->'b)  =
    async{
        let mutable res:'b= res
        for i in 0..numtimes do
            res<-(fn())
            do! Async.Sleep(interval)
        return res
    }
///Writes to the stream for an amount of tie and then disposes it
let writeIntermittently (file:string) (interval:int) numtimes=
    async{
        use a = File.AppendText(file)
        printfn "about to start writing"
        return! doIntermittently interval numtimes 0L 
                (fun ()->
                    a.WriteLine "heyy"
                    a.Flush()
                    System.Diagnostics.Stopwatch.GetTimestamp()
                )
            
         
    }
(* let simplecheck name (time:int)=
    let mutable cont=true
    let mutable lastInfo=new FileInfo(name)
    while cont do
       printfn "n old = %i " lastInfo.Length
       Async.RunSynchronously <|Async.Sleep(time)
       let newInfo= new FileInfo(name)
       printfn "new=%i old = %i "newInfo.Length lastInfo.Length
       if newInfo.Length= lastInfo.Length then
            cont<-false *)
    
let testInterval checker writeInterval writes (checkInterval:int)  =
    async{
        let name= "./testInternal/testfile.test"
        Directory.CreateDirectory("./testInternal") |>ignore
        let a=File.WriteAllText(name,"sart")
        let source= new CancellationTokenSource()
        let ct= source.Token
        source.CancelAfter(writeInterval*(writes+1)+(checkInterval*3))
        let! writer=Async.StartChild (writeIntermittently name writeInterval writes)
        let! checker= checkAvailability checker name ct checkInterval
        let availableTime=Diagnostics.Stopwatch.GetTimestamp()
        if ct.IsCancellationRequested then 
            failwith "Took to long and had to call cancelation on checking. this means the check never sees it is avaialable"
        let! writeFinished= writer
        if availableTime>writeFinished then 
            return true
        else 
            return false
    }|>Async.RunSynchronously
    
///Test the availability checker with an interval that is to short and will result in the checker thinking he write is complete 
let testIncorrectInterval checker  =
        testInterval checker 150 10 90
///Test the availability checker with the apropriate checking interval
let testCorrectInterval checker  =
        testInterval checker 100 10 150

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
        test "availablility size threshold"{
            let res= 
                async{
                    let! task=writeIntermittently "./test.test" 10 1000 |>Async.StartChild
                    do! Async.Sleep 1000
                    let token= new CancellationTokenSource()
                    let! avail=checkAvailability (Checks.checkFileSizeThreshold 1000) "./test.test" token.Token 10
                    token.CancelAfter (TimeSpan.FromSeconds 20 )
                    let! res= task
                    return avail
                }
                |>Async.RunSynchronously

            Expect.isTrue ((runintestEnv (fun x->res))=Availability.Available) "File execeded size but threshold did not trigger"
        }
        test "availablility size threshold not surpassed"{
            let res= 
                async{
                    let! task=writeIntermittently "./test.test" 1000 100|>Async.StartChild
                    do! Async.Sleep 1000
                    let token= new CancellationTokenSource()
                    let! avail=checkAvailability (Checks.checkFileSizeThreshold 10000) "./test.test" token.Token 10
                    token.CancelAfter (TimeSpan.FromSeconds 50 )
                    let! res= task
                    return avail
                }
                |>Async.RunSynchronously

            Expect.isTrue ((runintestEnv (fun x->res))=Availability.Cancelled) "File execeded size but threshold did not trigger"
        }
        test "availability file size check correct interval"{
            let res=  (fun ()-> testCorrectInterval checkFileSize)
                
            Expect.isTrue(runintestEnv res) "File was considered available after writer had finished writing"
        };
        test "availability file size check incorrect interval"{
            let res=  (fun ()-> testIncorrectInterval checkFileSize)
                
            Expect.isFalse(runintestEnv res) "File was considered available during writing, when the checking interval was too small, as expected"
        }
        test "availability open Stream check correct interval"{
            let res=  (fun ()-> testCorrectInterval checkFileStream)
        
            Expect.isTrue(runintestEnv res) "File was considered available after writer had finished writing"
        };
        test "availability open Stream size check incorrect interval"{
            let res=  (fun ()-> testIncorrectInterval checkFileStream)
        
            Expect.isTrue(runintestEnv res) "File Stream check not effectedbyt to small interval"
        };
        // test "availability size and write time"{
        //     let res=  (fun ()-> testCorrectInterval checksi)
        
        //     Expect.isTrue(runintestEnv res) "File Stream check not effectedbyt to small interval"
        // }
    ]
