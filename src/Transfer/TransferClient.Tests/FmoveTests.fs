module FmoveTests

open System
open System.Diagnostics


open Mover.Types
open Mover.FileMove
open System.Threading
open System.IO
open Expecto
open Expecto
open Utils
//-----------Prep---------
//let init()=IO.File.Copy(source, source + ".back",true)
let init()=setup()
//---------------------
//let reset()=IO.File.Move(source + ".back", source,true)
let reset()=
    try Directory.Delete("./testSource",true)
    with|_->()
    try Directory.Delete("./testDest", true)
    with|_->()
let testBase test=

    init()
    test()
    reset()



let standardCopyTime =
    testBase (fun ()->
        let timer = Stopwatch()
        let Callback = (fun x -> ())
        let ct = new Threading.CancellationTokenSource()
        timer.Start()
        let a = FCopy source destFile Callback ct.Token
        timer.Stop()
        timer.ElapsedMilliseconds
        ()
    )


let basicCopy input =
    let copy = FCopy source destFile
    let thisCallback = (fun x -> ())

    let thisCt =
        (new Threading.CancellationTokenSource()).Token

    let task =
        match input with
        | CTCallBack (ct, callBack) -> copy callBack ct
        | CT ct -> copy thisCallback ct
        | CallBack cb -> copy cb thisCt
        | Basic -> copy thisCallback thisCt

    task
[<Tests>]
let tests=
    
    testSequenced <|testList " Fmove Tests"[
    test "Fmove Cancels in under two seconds" {
        testBase (fun ()->
            
            let timer = Diagnostics.Stopwatch()
            let ct = new Threading.CancellationTokenSource()
            timer.Start()
            let cancelAfter=1000L
            ct.CancelAfter(int cancelAfter)

            Async.RunSynchronously (basicCopy (CT ct.Token),int cancelAfter+5000)
            timer.Stop()
            //Timer job should finish in under one second after cancellation
            Utils.logInfof "time taken: %i" timer.ElapsedMilliseconds
            timer.ElapsedMilliseconds|>Expect.isGreaterThan <|cancelAfter <|"cancelled in greater than 1 "
            timer.ElapsedMilliseconds|>Expect.isLessThan <|(cancelAfter+1000L)<|"Cancelled in under 2 seconds"
        )
    }

    test "Fmove Deletes on cancellation "{
        testBase (fun ()->
            let ct = new CancellationTokenSource()
            ct.CancelAfter(3000)
            basicCopy (CT ct.Token)
            Expect.isFalse (IO.File.Exists(destFile)) "file doesn't exist"
            )
    }

    test "Fmove calls Progress more than 10 times"{
        testBase (fun ()->
            let mutable callCount = 0
            let Callback = (fun x -> callCount <- callCount + 1)
            let ct = new Threading.CancellationTokenSource()
            let a = FCopy source destFile Callback ct.Token
            ct.CancelAfter(3000)
            printfn "Res:%A" (Async.RunSynchronously a)
            callCount |>Expect.isGreaterThan <|  1<| "send progress event"
        )
    }


    ]