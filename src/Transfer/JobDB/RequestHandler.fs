module JobManager.RequestHandler

open FSharp.Control.Reactive
open System
open System.Threading.Channels
open FSharp.Control
open LoggingFsharp
module MessageRequestHandler=
    type Message<'a> = (unit -> 'a)*AsyncReplyChannel<Result<'a,Exception>>
    let inline handler (msg:Message<'a>)=
        let actionToRun,replyChannel = msg
        try 
            let res =
                actionToRun ()
            replyChannel.Reply(Ok res)
        with e->replyChannel.Reply(Error e)
    
    let inline messageLoop(inbox:MailboxProcessor<Message< ^a>>)=
        let rec loop()= async{
            
            let! msg= inbox.Receive()
            handler msg
            return! loop()
        }
        loop()
    let inline processor()=MailboxProcessor.Start(messageLoop)
    let  proc=MailboxProcessor.Start(messageLoop)

    let inline doRequest (handlerAgent:MailboxProcessor<Message<'g>>) (f:'a->'c) a :Async<'c>=
            async{
            let func= (fun ()-> ((f a) :>obj ) )
            let! res=handlerAgent.PostAndAsyncReply((fun reply->func,reply))
            let ret=
                match res with
                |Ok(x)->x
                |Error(e)->raise (exn("'Request handler'Requested function threw exception during execution",e))
            return ret:?>'c
            
            }
    let inline doRequestSync (handlerAgent:MailboxProcessor<Message<'g>>) (f:'a->'c) a :'c=
            let func= (fun ()-> (f a :>obj) )
            let res=handlerAgent.PostAndReply((fun reply->func,reply)) 
            let ret=
                match res with
                |Ok(x)->x
                |Error(e)->raise (exn("'Request Handler'Requested function threw exception during execution",e))
            ret:?>'c
(*     module Global=
        let doRequest:('a->'b) ->'a ->Async<'b>= doRequest processor 
        
        let doRequestSync a b = doRequestSync processor a b *)
[<AbstractClass>]
type RequestHandler()=
    abstract member doSyncReq: ('a->'c)-> 'a ->'c
    abstract member doRequest: ('a->'c)-> 'a ->Async<'c>
    abstract member handle :unit->unit

type MessageRequestHandler()=
    inherit RequestHandler()
    let processor=MailboxProcessor.Start(MessageRequestHandler.messageLoop)

    override x.doSyncReq f a = MessageRequestHandler.doRequestSync processor f a
    override x.doRequest f a = MessageRequestHandler.doRequest processor f a
    override x.handle()=() 

///DO NOT USE, DOesn't seem to actually work
module LockingRequestHandler=
    //open System.Reactive.Linq
    ///Contains a request to do an operation on the job database
    type Requests = Event<( (unit -> Object)*ChannelWriter<Object> )>
    ///A callback that will be triggered once the requsted operation si complete
    type RequestComplete<'T> = Event<'T>
    // The particular object is irrelivant. it is simply used in the lock.
    let lockObj=obj();
    ///Handles incoming requests and executes them one by one
    let handleRequests (requests: Requests) =
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
        //Lgdebugf "{Request Handler} Running JobDB Interaction "
        let finished = Channel.CreateUnbounded<Object>() //TODO:This is proabbly very unperforamnt. it may be worth poolnig the channels 
        reqQueue.Trigger ((fun ()-> (f a ):>Object ),finished.Writer)
        let res= finished.Reader.ReadAsync().AsTask()|>Async.AwaitTask|>Async.RunSynchronously
        //let res=finished.Publish|>Observable.head|> Observable.wait 
        res :?>'c
type LockingRequestHandler()=
    inherit RequestHandler()
    let processor=LockingRequestHandler.Requests()
    let _=LockingRequestHandler.handleRequests processor
    override x.doSyncReq f a = LockingRequestHandler.doSyncReq processor f a
    override x.doRequest f a = LockingRequestHandler.doRequest processor f a
    override x.handle()=() 
