namespace Transfer

open System.IO
open System.Threading
open System
open Transfer.Data
open SharedData
module Scheduler =
    let isAvailabe source =
        async {
            let mutable currentFile = new FileInfo(source)
            let mutable unavailable = true
            while unavailable do
                try
                    using (currentFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) (fun stream ->
                        stream.Close())
                    unavailable <- false
                with :? IOException ->
                    do! Async.Sleep(1000)
                    unavailable <- true
        }

    let scheduleTransfer isFTP destination source (guid: Guid) eventHandler =
        async {
            guid|>setTransferData
                    { Percentage = 0.0
                      FileSize = float(FileInfo(source).Length/int64 1000/int64 1000)
                      FileRemaining = 0.0
                      Speed = 0.0
                      Destination = destination
                      Source = source
                      StartTime = DateTime.Now
                      id = guid
                      Status = TransferStatus.Waiting 
                      EndTime=DateTime.Now} 
            let ct = new CancellationTokenSource()
            printfn "Scheduled transfer from %s To-> %s" source destination
            CancellationTokens.Add(guid, ct)
            do! isAvailabe source
            return Mover.MoveFile isFTP destination source guid eventHandler ct
        }
