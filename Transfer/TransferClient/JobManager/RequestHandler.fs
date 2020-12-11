module TransferClient.JobManager.RequestHandler


open System.Collections.Generic
open TransferClient.JobManager.Main
open SharedFs.SharedTypes
open FSharp.Control.Reactive
open System
open System.Threading
//open System.Reactive.Linq
///Contains a request to do an operation on the job database
type Requests = Event<( (unit -> Object)*Event<Object> )>
///A callback that will be triggered once the requsted operation si complete
type RequestComplete<'T> = Event<'T>
///Handles incoming requests and executes them one by one
let requestHandler (requests: Requests) =
    requests.Publish
    |> Observable.subscribe (fun ( b,a) ->
        let res = b ()
        a.Trigger(res)
        ())
///schedules a job to interact with the database and returns an async function to return the result
let doRequest (req: Requests) (f:'a->'c) a =
    async{
        let finished = RequestComplete<Object>()
        req.Trigger ((fun ()-> (f a ):>Object ),finished)
        let! a=Async.AwaitEvent(finished.Publish)
        return a:?>'c
    }
let doSyncReq (req:Requests) (f:'a->'c) a =
    let finished = RequestComplete<Object>()
    req.Trigger ((fun ()-> (f a ):>Object ),finished)
    Async.AwaitEvent(finished.Publish)|>Async.RunSynchronously:?>'c