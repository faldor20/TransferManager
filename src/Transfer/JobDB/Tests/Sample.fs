module JobManager.Tests.RequestHandler

open Expecto
open System.Diagnostics
open JobManager.RequestHandler
open Expecto.BenchmarkDotNet
open BenchmarkDotNet
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open FSharp.Control
open System
open System.Threading.Tasks
module mb=
  type Message<'a> = (unit -> 'a)*AsyncReplyChannel<'a>
  let  handler (msg:Message<'a>)=
      let actionToRun,replyChannel = msg
      let res = actionToRun ()
      replyChannel.Reply(res)

  let messageLoop(inbox:MailboxProcessor<Message< ^a>>)=
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


let isPrime a=
  let start=a/2
  [1..start]|>List.tryFind(fun x->
  a%x=0)
  |>Option.isSome
let calcPrimes start number= 
  [start..(number+start)]|>List.map(fun x-> x,isPrime x)
[<MemoryDiagnoser>]
type RequesthandlerBenchMark()=
  let requestQueue=LockingRequestHandler() 
  let processor= MailboxProcessor.Start(mb.messageLoop)
  [<Params(100)>]
  member val numArgs=1 with get,set
  [<Params(1)>]
  member val MinPrime=1 with get,set
  [<Benchmark>]
  member x.reuestQueue()= 
    async{

      for i in [x.numArgs..x.numArgs+x.MinPrime] do
        let! a= (requestQueue.doRequest (fun ()-> isPrime i) ())
        ()
      
      ()
    }|>Async.RunSynchronously
  [<Benchmark>]
  member x.message()= 
    async{
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
    
let infinterandom min max=
  seq{
    let rand=System.Random()
    while true do
      yield ( rand.Next(min,max))
  }
let delay (a:int)=
  Async.RunSynchronously (Async.Sleep(a))



let requestsExecutedInOrder (waits:int list) handler=
  let doer:RequestHandler= handler
  let watch=Stopwatch()
  let delyThenTime x=
    delay x
    watch.Elapsed
  watch.Start()
  let times=
    waits
    |>List.map(fun x-> 
        let task=new Task<TimeSpan>((fun ()-> 
          printfn "starting"
          doer.doSyncReq delyThenTime x))
        task.Start()
        task )
    |>List.map (fun x->x.Result)
  let happenedInOrder=times|>List.pairwise|>List.map(fun (x,y)-> 
    printfn "time diff %A"( x-y).Milliseconds
    x <y
    )
  let correctlyOrdered= happenedInOrder|>List.tryFind ((=)false) |>Option.isNone
  correctlyOrdered


let requestsExecutedInOrder2 (waits:int list) handler=
  let doer:RequestHandler= handler
  let watch=Stopwatch()
  let delyThenTime x=
    delay x
    watch.Elapsed
  watch.Start()
  let times=
    waits
    |>AsyncSeq.ofSeq
    |>AsyncSeq.mapAsync(fun x-> 
          printfn "starting"
          Async.StartChild (doer.doRequest delyThenTime x))
    |>AsyncSeq.mapAsyncParallel(fun x->x )
    |>AsyncSeq.toListSynchronously
  let happenedInOrder=times|>List.pairwise|>List.map(fun (x,y)-> 
    printfn "time diff %A"(x.TotalMilliseconds ,y.TotalMilliseconds)
    x <y
    )
  let correctlyOrdered= happenedInOrder|>List.tryFind ((=)false) |>Option.isNone
  correctlyOrdered
let waitList=[1;2;1;100;2;1;1;1;10;1;1;2;]


[<Tests>]
let tests =
  testSequenced<|testList "RequestHandler" [
(*     test "Benchmark" {
      
      
      benchmark<RequesthandlerBenchMark> benchmarkConfig  (fun _-> null) |> ignore
    } *)
    test "exceptions still allow completion"{
      let handler= (MessageRequestHandler())
      let res=
        async{
          let! a =handler.doRequest delay 50|>Async.StartChild
          delay 10
          let! b= handler.doRequest delay 10|>Async.StartChild
          let! c= async{
              try 
                return!  handler.doRequest (fun x-> delay 10; raise (exn "exception") ) 1
              with e-> 
                printfn "Error caught: \n %A" e
                return async{()}
              }
          let! d= handler.doRequest (fun x-> delay x; x) 10|>Async.StartChild
          do! a
          do! b
          do! c
          let! out=d
          return true
        }|>Async.RunSynchronously
      Expect.isTrue res "The Handler must have broken somehow"
    }
    test "Message request handler ordered" {
      let correctlyOrdered= requestsExecutedInOrder2 waitList (MessageRequestHandler())
      Expect.isTrue correctlyOrdered "all actions eecuted in order"
    }
    test "Locking request handler ordered" {
      let correctlyOrdered= requestsExecutedInOrder2 waitList (LockingRequestHandler())
      Expect.isTrue correctlyOrdered "all actions eecuted in order"
    }
  ]
