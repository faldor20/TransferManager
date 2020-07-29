namespace TransferClient.IO

open System
open System.IO
open System.Threading
open Types
module FileMove =
    type ProgressData={
        Progress:float
        BytesTransfered:int64
        SpeedMB:float
    }

    ///
    /// progress takes copyprogress and speed
    let FCopy (source:string) destination progress (ct:CancellationToken) =
        async{
            let dest=
                match File.GetAttributes destination with
                |FileAttributes.Directory->
                    destination+ (Path.GetFileName source)
                |_-> destination
            let array_length = int (Math.Pow(2.0, 19.0))
            let dataArray: array<byte> = Array.zeroCreate (array_length)
            use  fsread=(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None, array_length)) 

            use bwread=(new BinaryReader(fsread)) 

            use fswrite=(new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, array_length)) 

            use bwwrite=(new BinaryWriter(fswrite)) 
                            
            let time=Diagnostics.Stopwatch()
            time.Start()
            let mutable reads = 0.0
            let mutable speedMB= 0.0
            let mutable lastReads=0.0
            let requiredReads =
                double fsread.Length / double array_length
            let speedInterval=2.0
            let speedTimer = new Timers.Timer(speedInterval*1000.0) //check speed every two seconds
            let timer = new Timers.Timer(500.0) //check progress every half second
            speedTimer.Elapsed.Add(fun arg->
                speedMB<- ((reads-lastReads)/2.0)/speedInterval   )

            timer.Elapsed.Add(fun args -> 
                progress
                    {
                    Progress= ((reads / requiredReads) * 100.0)
                    BytesTransfered= (int64 reads*int64 array_length)
                    SpeedMB= speedMB
                    }
            )
            let readBytes()=
                let rec loop () =
                    if(ct.IsCancellationRequested) then  TransferResult.Cancelled
                    else
                        let read = bwread.Read(dataArray, 0, array_length)
                        reads <- reads + 1.0
                        if 0 = read then
                            TransferResult.Success
                        else
                            bwwrite.Write(dataArray, 0, read)
                            loop ()
                        
                try loop()
                with
                |_->TransferResult.Failed
            return readBytes ()
                        }
