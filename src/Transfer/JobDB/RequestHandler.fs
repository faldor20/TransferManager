module JobManager.RequestHandler

open FSharp.Control.Reactive
open System
open System.Threading.Channels
open FSharp.Control
open LoggingFsharp
//open System.Reactive.Linq
///Contains a request to do an operation on the job database
type Requests = Event<( (unit -> Object)*ChannelWriter<Object> )>
///A callback that will be triggered once the requsted operation si complete
type RequestComplete<'T> = Event<'T>
// The particular object is irrelivant. it is simply used in the lock.
let lockObj=obj();
///Handles incoming requests and executes them one by one
let requestHandler (requests: Requests) =
    Lginfof "{JobManager} Starting request handler"
    requests.Publish
    //This will run in paralell. We use a lock to prevent that
    |> Observable.subscribe (fun ( actionToRun,outputChannel) ->  
     
        lock lockObj (fun ()-> 
            let res = actionToRun ()
            outputChannel.WriteAsync(res).AsTask().Wait()
        )
       
        ())
///schedules a job to interact with the database and returns an async function to return the result
let doRequest (req: Requests) (f:'a->'c) a =
    async{
        let finished = Channel.CreateUnbounded<Object>()
        req.Trigger ((fun ()-> (f a ):>Object ),finished.Writer)
        let! a=finished.Reader.ReadAsync().AsTask()|>Async.AwaitTask
        return a:?>'c
    } 
///schedules a job to interact with the database and returns an async function to return the result
let doSyncReq (reqQueue:Requests) (f:'a->'c) a =
    Lgdebugf "{Request Handler} Running JobDB Interaction "
    let finished = Channel.CreateUnbounded<Object>() //TODO:This is proabbly very unperforamnt. it may be worth poolnig the channels 
    reqQueue.Trigger ((fun ()-> (f a ):>Object ),finished.Writer)
    let res= finished.Reader.ReadAsync().AsTask()|>Async.AwaitTask|>Async.RunSynchronously
    //let res=finished.Publish|>Observable.head|> Observable.wait 
    
    res :?>'c