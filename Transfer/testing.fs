namespace Transfer
open System
open System.Threading
open FSharp.Control.Reactive
open FSharp.Control
//open System.Reactive
module Testing=
    let a=1


   

    let randomwait time=
        async{
            printfn "started waiting : %i"time
            do! Async.Sleep(time*1000);
            printfn "waited %i" time
            return time
        }

    let stream=
        asyncSeq{
            for i=10 downto 1 do
                let waitTime= i
                yield randomwait waitTime
        }
    let res=
        let event= new Event<int>()
        let task=stream 
                |> AsyncSeq.iterAsyncParallel (fun x-> 
                    async{ 
                            let! x'=x
                            event.Trigger x'
                            })
        (task,event)
    let run =    
        let task,event= res
        event|> Observable.subscribe (fun x->printfn "print Time %i"x)
        0
        
   (*  let client= FluentFTP.FtpClient.Connect("***REMOVED***")
    client.Connect()
    client.DirectoryExists("/hvy_bun_news/Transfer/") *)
(*     let moveTest  out source=
            async{
            let handler (data:Data.TransferData) guid=
                    printf "Copied %i%% of %s" (int data.Percentage ) source
                    printfn " At speed: %i MB/s" data.Speed
            let task= MoveFile 1000.0 out source (Guid.NewGuid()) handler
            let! result,guid= Async.AwaitTask task
            printfn "done, guid: %A" guid
            return (guid,source) 
            } *)

(*     let asyncTest= 
        let task2 = async{
           let a=moveTest "F:/test" "./rawPal.mxf"
           let b=moveTest "F:/test" "./testSource/Airships.Conquer.the.Skies.v1.0.15.4.rar"
           let c=moveTest "F:/test" "./testSource/emacs-27.0.60-snapshot-emacs-27-2020-02-10-x86_64-installer.exe"
           let d=moveTest "F:/test" "./testSource/nixos-minimal-19.09.1019.c5aabb0d603-x86_64-linux.iso"

           printfn "1=%A"  a
           printfn "2=%A" b
           printfn "3=%A" c
         
        }
        Async.RunSynchronously task2 *)