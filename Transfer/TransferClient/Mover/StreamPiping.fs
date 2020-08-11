namespace TransferClient.IO
open System
open System.Diagnostics;

open System.Threading
open System.IO

open System.Threading.Tasks
open System.IO.Pipelines
open FSharp.Control.Tasks
open System.Buffers
module StreamPiping=
    let private fillPipe (source:Stream) (writer:PipeWriter)=
        task{
            let minimumBufferSize = int (Math.Pow(2.0,10.0))
            let mutable reading=true
            while reading do
                let memory =  (writer.GetMemory(minimumBufferSize))
                let! bytesRead = source.ReadAsync( memory)
                if bytesRead = 0 then
                    reading<-false
                // Tell the PipeWriter how much was read from the Socket.
                writer.Advance(bytesRead);
                let! result = writer.FlushAsync()
               
                
                if result.IsCompleted then
                    reading<-false
            do! writer.CompleteAsync()
        }  
    let private readPipe (output:Stream) (reader:PipeReader)=
        task{
            let mutable reading=true
            while reading do
                let! result =  reader.ReadAsync();
                let mutable buffer = result.Buffer;
                let mutable line =System.Buffers.ReadOnlySequence<byte>()
                let tryReadLine ()=
                    // Look for a EOL in the buffer.
                    let position = buffer.PositionOf((byte)'\n');
                
                    if  not(position.HasValue) then
                        line<- Buffers.ReadOnlySequence<byte>() 
                        false 
                    else
                        // Skip the line + the \n.
                        line <- buffer.Slice(0, position.Value)
                        buffer <- buffer.Slice(buffer.GetPosition(1L, position.Value))
                        true

                while tryReadLine() do
                    // Process the line.
                    do! output.WriteAsync(line.ToArray(),0,(int)line.Length)

                // Tell the PipeReader how much of the buffer has been consumed.
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if result.IsCompleted then
                    reading<-false
            do! reader.CompleteAsync();
        }
    let pipeStream source dest= 
        let pipe = new Pipe();
        task{
            let writing = fillPipe source pipe.Writer
            let reading = readPipe dest (pipe.Reader)
            do!writing
            do!reading
        }

    
    let simplewriter (source:Stream) (dest:Stream)=
        task{
        let array_length = int (Math.Pow(2.0, 19.0))

        let dataArray: array<byte> = Array.zeroCreate (array_length)
        //this needs to be used if the ftp streamash been set to binary
        //use bwWrite=new BinaryWriter(dest)
        let mutable keepReading = true
        while keepReading do
            let! read = source.AsyncRead(dataArray, 0, array_length)
            if 0 = read then
                //Finish reading
                keepReading<- false
            else
               // bwWrite.Write(dataArray, 0, read)
                do!dest.AsyncWrite(dataArray, 0, read)
                //Continue reading
                keepReading <- true
        }
    let simpleBinaryWriter (source:Stream) (dest:Stream)=
        task{
        let array_length = int (Math.Pow(2.0, 19.0))
        let dataArray: array<byte> = Array.zeroCreate (array_length)
        //this needs to be used if the ftp streamash been set to binary
        use bwWrite=new BinaryWriter(dest)
        let mutable keepReading = true
        while keepReading do
            let! read = source.AsyncRead(dataArray, 0, array_length)
            if 0 = read then
                //Finish reading
                keepReading<- false
            else
                bwWrite.Write(dataArray, 0, read)
                do!dest.AsyncWrite(dataArray, 0, read)
                //Continue reading
                keepReading <- true
        }