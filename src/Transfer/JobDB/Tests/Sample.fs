module JobManager.Tests.RequestHandler

open Expecto
open System.Diagnostics
open JobManager.RequestHandler
open Expecto.BenchmarkDotNet
open BenchmarkDotNet
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open FSharp.Control
module mb=
  type Message<'a> = (unit -> 'a)*AsyncReplyChannel<'a>
  let  handler (msg:Message<'a>)=
      let actionToRun,replyChannel = msg
      let res = actionToRun ()
      replyChannel.Reply(res)

  let   messageLoop(inbox:MailboxProcessor<Message< ^a>>)=
      let rec loop()= async{
          let! msg= inbox.Receive()
          handler msg
          return! loop()
      }
      loop()
  let  doRequest (handlerAgent:MailboxProcessor<Message<'c>>) (f:'a->'c) a :Async<'c>=
          let func= (fun ()-> (f a ) )
          handlerAgent.PostAndAsyncReply((fun reply->func,reply))
      
  let  doRequestSync (handlerAgent:MailboxProcessor<Message<'c>>) (f:'a->'c) a :'c=
          let func= (fun ()-> (f a ) )
          handlerAgent.PostAndReply((fun reply->func,reply))
        
  let processor=MailboxProcessor.Start(messageLoop)
type requestDoer()=
  let a= Requests()
  let _handler=requestHandler(a)
(*   let runtime=  System.Threading.Tasks.Task.Run((fun ()->
    printfn "running request handler"
    _handler)) *)
  member x.event= a
  member x.handler=_handler
  member x.processrequest f a = doRequest(a) (f) (a) 

let isPrime a=
  let start=a/2
  [1..start]|>List.tryFind(fun x->
  a%x=0)
  |>Option.isSome
let calcPrimes start number= 
  [start..(number+start)]|>List.map(fun x-> x,isPrime x)
  System.Threading.Tasks.Task.Delay(1).Wait()

type RequesthandlerBenchMark()=
  let requestQueue=requestDoer()
  
  [<Params(10,100)>]
  member val numArgs=1 with get,set
  [<Params(10)>]
  member val MinPrime=1 with get,set
  [<Benchmark>]
  member x.reuestQueue()= 
    async{
      let requestQueue=requestDoer()

      for i in [x.numArgs..x.numArgs+x.MinPrime] do
        let! a= (doRequest (requestQueue.event) (fun ()-> isPrime i) ())
        ()
      
      ()
    }|>Async.RunSynchronously
  [<Benchmark>]
  member x.message()= 
    async{
      let processor=MailboxProcessor.Start(mb.messageLoop)
      for i in [x.numArgs..x.numArgs+x.MinPrime] do
        let! a= (mb.doRequest (processor) (fun ()-> isPrime i) ())
        ()
      ()
    }|>Async.RunSynchronously
  [<Benchmark>]
  member x.Async()=
    async{
      for i in [x.numArgs..x.numArgs+x.MinPrime] do
        let! a= (async{return isPrime i})
        ()
      ()
    }|>Async.RunSynchronously
  [<Benchmark(Baseline=true)>]
  member x.Normal()=
      for i in [x.numArgs..x.numArgs+x.MinPrime] do
        let a=  isPrime i
        ()
      ()
    

[<Tests>]
let tests =
  testSequenced<|testList "RequestHandler" [
    test "Benchmark" {
      benchmark<RequesthandlerBenchMark> benchmarkConfig (fun _ -> null) |> ignore
    }
  ]
