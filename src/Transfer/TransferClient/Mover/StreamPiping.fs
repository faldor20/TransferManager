namespace TransferClient.IO
open System
open System.IO
open System.IO.Pipelines
open FSharp.Control.Tasks
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
                let mutable line =new ReadOnlyMemory<byte>()
                let tryReadLine ()=
                //this took fucking forever to make work. from what i understand you have to try to read an amount of a certain size and if you read bove a 
                //speicif size you get nothing so i can't read the whole buffer, i cant read a set amount i have to read one lines worth at a time
                        line <- buffer.First
                        if line.IsEmpty then 
                            false
                        else
                            buffer <- buffer.Slice(buffer.GetPosition(int64 line.Length))
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