module TransferClient.JobManager.RequestHandler


open FSharp.Control.Reactive
open System
open System.Threading.Channels
//open System.Reactive.Linq
///Contains a request to do an operation on the job database
type Requests = Event<( (unit -> Object)*ChannelWriter<Object> )>
///A callback that will be triggered once the requsted operation si complete
type RequestComplete<'T> = Event<'T>
///Handles incoming requests and executes them one by one
let requestHandler (requests: Requests) =
    TransferClient.Logging.infof "{JobManager} Starting request handler"
    requests.Publish
    |> Observable.subscribe (fun ( b,a) ->  
        let res = b ()
        a.WriteAsync(res).AsTask().Wait()
        ())
///schedules a job to interact with the database and returns an async function to return the result
let doRequest (req: Requests) (f:'a->'c) a =
    async{
        let finished = Channel.CreateUnbounded<Object>()
        req.Trigger ((fun ()-> (f a ):>Object ),finished.Writer)
        let! a=finished.Reader.ReadAsync().AsTask()|>Async.AwaitTask
        return a:?>'c
    } 
let doSyncReq (req:Requests) (f:'a->'c) a =
    let finished = Channel.CreateUnbounded<Object>() //TODO:This is proabbly very unperforamnt. it may be worth poolnig the channels 
    req.Trigger ((fun ()-> (f a ):>Object ),finished.Writer)
    let res= finished.Reader.ReadAsync().AsTask()|>Async.AwaitTask|>Async.RunSynchronously
    //let res=finished.Publish|>Observable.head|> Observable.wait 
    
    res :?>'c