module Tests

open TransferClient
open System
open System.Diagnostics
open Xunit
open TransferClient.IO.FileMove
open System.Threading
open SharedFs.SharedTypes
open System.IO
open Xunit.Abstractions

let source = @".\testSource\File"

let destFile = @".\testDest\File"
let dest = @".\testDest\"
try Directory.Delete("./testSource",true)
with|_->()
try Directory.Delete("./testDest", true)
with|_->()
//setup environ
let setup()=
    
        Directory.CreateDirectory(@".\testSource\")|>ignore
        Directory.CreateDirectory(@".\testDest\")|>ignore
        let MakeFile()=
            
            let fs = new FileStream(@".\testSource\File", FileMode.CreateNew);
            fs.Seek(512L * 1024L * 1024L, SeekOrigin.Begin)|>ignore //500 MB file
            fs.WriteByte(byte 0);
            fs.Close();
        try 
            MakeFile()
        with|_ ->printfn "makefile faileed"
setup()


type SignalRTest(output:ITestOutputHelper) =
    let write result =
        output.WriteLine (sprintf "The actual result was %O" result)

    let localDBAcess = DataBase.LocalDB.AccessFuncs
    let sampleData: TransferData =
            { Destination = dest
              Source = source
              FileRemaining=10.0
              ID=0
              FileSize=10.0
              EndTime=DateTime.Now
              GroupName="hi"
              Speed = 0.0
              StartTime = System.DateTime.Now
              ScheduledTime = DateTime.Now 
              Status=TransferStatus.Waiting
              Percentage=0.0
              }

    let setupDB() =
       DataBase.LocalDB.addTransferData "hi" sampleData
    let resetDB() =
       DataBase.LocalDB.localDB<- System.Collections.Generic.Dictionary()

    [<Fact>]
    let ``Signalr reset clears database``()=
        setupDB()|>ignore
        Assert.NotEmpty DataBase.LocalDB.localDB
        SignalR.ClientApi.ResetDB.Invoke()
        Assert.Empty DataBase.LocalDB.localDB
type CopyParams =
    | CTCallBack of CancellationToken * (ProgressData -> unit)
    | CT of CancellationToken
    | CallBack of (ProgressData -> unit)
    | Basic
type FmoveTests(output:ITestOutputHelper) =
    let write result =
        output.WriteLine (sprintf "The actual result was %O" result)
    //-----------Prep---------
    let init()=IO.File.Copy(source, source + ".back")
    //---------------------
    let reset()=IO.File.Move(source + ".back", source)
    let testBase test=
        init()
        test()
        reset()

    [<Fact>]
    let ``My test`` () = Assert.True(true)

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

        (Async.RunSynchronously task)

    [<Fact>]
    let ``Fmove Cancels in under one second`` () =
        testBase (fun ()->
            let timer = Diagnostics.Stopwatch()
            let ct = new Threading.CancellationTokenSource()
            timer.Start()
            ct.CancelAfter(3000)
            basicCopy (CT ct.Token)
            timer.Stop()
            //Timer job should finish in under one second after cancellation
            Assert.True
                (int64 3000
                 <= timer.ElapsedMilliseconds
                 && timer.ElapsedMilliseconds < int64 4000)
        )

    [<Fact>]
    let ``Fmove Deletes on cancellation `` () =
        testBase (fun ()->
            let ct = new CancellationTokenSource()
            ct.CancelAfter(3000)
            basicCopy (CT ct.Token)
            Assert.False(IO.File.Exists(destFile)))

    [<Fact>]
    let ``Fmove calls Progress more than 10 times`` () =
        testBase (fun ()->
            let mutable callCount = 0
            let Callback = (fun x -> callCount <- callCount + 1)
            let ct = new Threading.CancellationTokenSource()
            let a = FCopy source destFile Callback ct.Token
            ct.CancelAfter(3000)
            printfn "Res:%A" (Async.RunSynchronously a)
            Assert.True(callCount > 10))


     