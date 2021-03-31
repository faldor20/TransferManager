module Utils

open TransferClient
open System
open System.Diagnostics


open TransferClient.IO.FileMove
open System.Threading
open SharedFs.SharedTypes
open Expecto.Logging
open System.IO
open Microsoft.FSharp.Core.Printf
let mutable logger = Log.create "Expecto"
let setLogName name = logger <- Log.create name
let logInfof fmt=
    ksprintf (logger.logSimple << Message.event Info )fmt
let logWarnf fmt=
    ksprintf (logger.logSimple << Message.event Warn )fmt
let lofErrorf fmt=
    ksprintf (logger.logSimple <<Message.event Error)fmt
let logVerbosef fmt=
   ksprintf (logger.logSimple << Message.event Verbose )fmt





let source = @".\testSource\File"
let destFile = @".\testDest\File"
let dest = @".\testDest\"

//setup environ
let setup()=
        try Directory.Delete("./testSource",true)
        with|_->()
        try Directory.Delete("./testDest", true)
        with|_->()
        Directory.CreateDirectory(@".\testSource\")|>ignore
        Directory.CreateDirectory(@".\testDest\")|>ignore
        let MakeFile()=
            
            let fs = new FileStream(@".\testSource\File", FileMode.CreateNew);
            fs.Seek(512L * 1024L * 1024L, SeekOrigin.Begin)|>ignore //500 MB file
            fs.WriteByte(byte 0);
            fs.Close();
        try 
            MakeFile()
        with|ex ->printfn "makefile faileed %s" ex.Message

type CopyParams =
    | CTCallBack of CancellationToken * (ProgressData -> unit)
    | CT of CancellationToken
    | CallBack of (ProgressData -> unit)
    | Basic