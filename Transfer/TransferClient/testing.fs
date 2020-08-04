namespace TransferClient
open System

module Testing=


    let test=
        async{
            let source2="./testSource2/Files2.zip" 
            let dest2= "H:/testDest2/Files2.zip"
            let source="./testSource2/Files.zip" 
            let dest= "H:/testDest2/Files.zip"
            let Callback=(fun x-> printfn "progress: %A"x)
            let ct= new Threading.CancellationTokenSource()
            let! jobA=IO.FileMove.FCopy  source2 dest2 Callback ct.Token|>Async.StartChild
            let! jobB=IO.FileMove.FCopy  source dest Callback ct.Token |>Async.StartChild
            let! resA=jobA
            let! resB=jobB
            return(resA,resB)
        }
        |>Async.RunSynchronously
        //let job=FileTransferManager.CopyWithProgressAsync ("./testSource2/Files.zip", "H:/testDest2/Files.zip", Callback,false )
       // Async.RunSynchronously (Async.AwaitTask job)
       (*  let inPath= "./testSource/BUNPREMIER.mxf"
        let fileName=IO.Path.GetFileName inPath
        let outPath="quantel:***REMOVED***@***REMOVED***/***REMOVED***Transfers/SSC to BUN/"+fileName
        Transcode inPath true outPath  *)
     