namespace TransferClient
open System

module Testing=


    let test=
        let source="./testSource2/Files.zip" 
        let dest= "H:/testDest2/Files.zip"
        let Callback=(fun x-> printfn "progress: %A"x)
        let ct= new Threading.CancellationTokenSource()
        let a=IO.FileMove.FCopy  source dest Callback ct.Token
        ct.CancelAfter(3000)
        printfn "Res:%A" (Async.RunSynchronously a)
        
        //let job=FileTransferManager.CopyWithProgressAsync ("./testSource2/Files.zip", "H:/testDest2/Files.zip", Callback,false )
       // Async.RunSynchronously (Async.AwaitTask job)
       (*  let inPath= "./testSource/BUNPREMIER.mxf"
        let fileName=IO.Path.GetFileName inPath
        let outPath="quantel:***REMOVED***@***REMOVED***/***REMOVED***Transfers/SSC to BUN/"+fileName
        Transcode inPath true outPath  *)
     