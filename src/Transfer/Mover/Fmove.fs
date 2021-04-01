

module Mover.FileMove 
open System
open System.IO
open System.Threading
open Types
open LoggingFsharp
type ProgressData =
    {   Progress: float
        BytesTransfered: int64
        SpeedMB: float }
    
let private writeWithProgress inStream outStream progress bufferLength (ct: CancellationToken)  =
    using (new BinaryReader(inStream)) (fun bwRead ->
            using (new BinaryWriter(outStream)) (fun bwWrite ->
                //----------Setup and progress reporting code--------
                let time = Diagnostics.Stopwatch()
                time.Start()
                    
                let mutable speedMB = 0.0
                //number of times a chunk of data of length "array_length" has been read
                let mutable reads = 0.0
                //used for calculating speed, the delta of reads each every second
                let mutable lastReads = 0.0

                //the number of reads that will need to be done to complete the transfer
                let requiredReads =
                    (double inStream.Length) / double bufferLength
                Lginfof "file size=  %i or %f MB"inStream.Length (double inStream.Length/1000.0/1000.0)
                let speedInterval = 1.0//check speed every "n" seconds

                use speedTimer = new Timers.Timer(speedInterval * 1000.0) 
                use timer = new Timers.Timer(500.0) //check progress every half second

                speedTimer.Elapsed.Add(fun arg ->
                        speedMB <- ((reads - lastReads) / 2.0) / speedInterval
                        lastReads<-reads)
                         

                timer.Elapsed.Add(fun args ->
                    progress
                        { 
                        Progress = ((reads / requiredReads) * 100.0)
                        BytesTransfered = (int64 reads * int64 bufferLength)
                        SpeedMB = speedMB 
                        })

                timer.Start()
                speedTimer.Start()

                //----------Copying Code--------
                //This is out read buffer
                let dataArray: array<byte> = Array.zeroCreate (bufferLength)

                let readBytes (bwread: BinaryReader) (bwwrite: BinaryWriter) =
                    //number of bytes actaully read into the buffer, if all has been read it will be 0
                    let read = bwread.Read(dataArray, 0, bufferLength)
                    reads <- reads + (double read/ double bufferLength)
                    if 0 = read then
                        //Finish reading
                        false
                    else
                        bwwrite.Write(dataArray, 0, read)
                        //Continue reading
                        true

                let mutable res = TransferResult.Success
                try
                    let mutable keepReading = true

                    while keepReading do
                        if (ct.IsCancellationRequested) then
                            res <- TransferResult.Cancelled
                            keepReading<-false
                        else
                            keepReading <- readBytes bwRead bwWrite
                with _ -> res <- TransferResult.Failed
                res
            ))

let private doTransfer inStream dest progress bufferLength (ct: CancellationToken) =
            
    using (new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferLength)) (fun fswrite ->
        writeWithProgress inStream fswrite progress bufferLength ct
        )

let private doFileTransfer source dest progress bufferLength (ct: CancellationToken)=
    using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None, bufferLength)) (fun fsread ->
    doTransfer fsread dest progress bufferLength (ct: CancellationToken)
    )
  
   
/// **Description**
/// A fast file system copy implimentation
/// 
/// `progress`: callback for copy progress takes copyprogress and speed
  
let FCopy (source: string) destination progress (ct: CancellationToken) =
    async {
        let dest =
            try
                // we use this wierd pattern beuase this enum is bit manipulated to allow for multiple states to be encoded at once.
                // each enum value is a bit. "1010" would mean two enums enum 1 and enum 4. The and just checks if the enum specified exists in the bits
                match File.GetAttributes destination with
                | x when (x &&& FileAttributes.Directory)=FileAttributes.Directory -> destination + (Path.GetFileName source)
                | _ -> destination
            //if the getatributes failes it musn't exist so it has to be a full filepath
            with|_->destination


        let buffLength = int (Math.Pow(2.0, 19.0))

        let res =
            doFileTransfer source dest progress buffLength ct

        let out =

            if (res = TransferResult.Cancelled
                || res = TransferResult.Failed) then
                try
                    printfn "Cancelled or failed deleting file %s" dest
                    File.Delete(dest)
                    res
                with _ ->
                    printfn "Cancelled or failed and was unable to delete output file %s" dest
                    TransferResult.Failed
            else
                res

        return out
    }
///
/// Same as FCopy but uses a stream as the input
let SCopy (source: Stream) fileName destination progress (ct: CancellationToken) =
    async {
        let dest =
            // we use this wierd pattern beuase this enum is bit manipulated to allow for multiple states to be encoded at once.
            // each enum value is a bit. "1010" would mean two enums enum 1 and enum 4. The and just checks if the enum specified exists in the bits
            match File.GetAttributes destination with
            | x when (x &&& FileAttributes.Directory)=FileAttributes.Directory -> destination + (fileName)
            | _ -> destination


        let buffLength = int (Math.Pow(2.0, 19.0))






        let res =
            doTransfer source dest progress buffLength ct

        let out =

            if (res = TransferResult.Cancelled
                || res = TransferResult.Failed) then
                try
                    printfn "Cancelled or failed deleting file %s" dest
                    File.Delete(dest)
                    res
                with _ ->
                    printfn "Cancelled or failed and was unable to delete output file %s" dest
                    TransferResult.Failed
            else
                res

        return out
    }