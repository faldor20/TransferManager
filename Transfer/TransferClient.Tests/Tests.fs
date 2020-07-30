module Tests
open TransferClient
open System
open System.Diagnostics
open Xunit
open TransferClient.IO.FileMove
open System.Threading

let source="./testSource/Files.zip" 
let dest= "./testDest/Files.zip"
//-----------Prep---------
IO.File.Copy(source, source+".back")
//---------------------

[<Fact>]
let ``My test`` () =
    Assert.True(true)

let standardCopyTime=
    let timer =Stopwatch()
    let Callback=(fun x->())
    let ct= new Threading.CancellationTokenSource()
    timer.Start()
    let a=FCopy  source dest Callback ct.Token
    timer.Stop()
    timer.ElapsedMilliseconds

type CopyParams=
    |CTCallBack of CancellationToken* (ProgressData -> unit)
    |CT of CancellationToken
    |CallBack of (ProgressData -> unit)
    |Basic
let basicCopy input =
    let copy= FCopy  source dest
    let thisCallback=(fun x->())
    let thisCt= (new Threading.CancellationTokenSource()).Token
    let task=
        match input with
        |CTCallBack (ct,callBack)->copy callBack ct
        |CT ct->copy thisCallback ct
        |CallBack cb-> copy cb thisCt 
        |Basic-> copy thisCallback thisCt
    
    (Async.RunSynchronously task)

[<Fact>]
let ``Fmove Cancels in under one second`` () =
    let timer=Diagnostics.Stopwatch()
    let ct= new Threading.CancellationTokenSource()
    timer.Start()
    ct.CancelAfter(3000)
    basicCopy (CT ct.Token)
    timer.Stop()
    //Timer job should finish in under one second after cancellation
    Assert.True(int64 3000<=timer.ElapsedMilliseconds&& timer.ElapsedMilliseconds<int64 4000)
[<Fact>]
let ``Fmove Deletes on cancellation `` () =
    let ct= new CancellationTokenSource()
    ct.CancelAfter(3000)
    basicCopy (CT ct.Token)
    Assert.False(IO.File.Exists(dest))

[<Fact>]
let ``Fmove calls Progress more than 10 times`` () =
    let mutable callCount=0
    let Callback=(fun x-> callCount<- callCount+1)
    let ct= new Threading.CancellationTokenSource()
    let a=FCopy source dest Callback ct.Token
    ct.CancelAfter(3000)
    printfn "Res:%A" (Async.RunSynchronously a)
    Assert.True(callCount>10)

//-----------Reset-----------
IO.File.Move(source+".back", source)
//---------------------
