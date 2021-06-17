
#r "nuget:FSharp.Control.AsyncSeq"
open System
open System.Threading.Channels
open FSharp.Control
type Message<'a> = (unit -> 'a)*AsyncReplyChannel<'a>
let inline handler (msg:Message<'a>)=
    let actionToRun,replyChannel = msg
    let res = actionToRun ()
    replyChannel.Reply(res)

let inline  messageLoop(inbox:MailboxProcessor<Message< ^a>>)=
    let rec loop()= async{
        let! msg= inbox.Receive()
        handler msg
        return! loop()
    }
    loop()
let inline doRequest (handlerAgent:MailboxProcessor<Message<'c>>) (f:'a->'c) a :Async<'c>=
        let func= (fun ()-> (f a ) )
        handlerAgent.PostAndAsyncReply((fun reply->func,reply))
    
let inline doRequestSync (handlerAgent:MailboxProcessor<Message<'c>>) (f:'a->'c) a :'c=
        let func= (fun ()-> (f a ) )
        handlerAgent.PostAndReply((fun reply->func,reply))
        
let processor=MailboxProcessor.Start(messageLoop)


///A callback that will be triggered once the requsted operation si complete
let delay(x:int)=
    Async.RunSynchronously(Async.Sleep(x))

printfn "hey"
let res=doRequestSync processor (fun a-> printfn "mulby10 delay "; delay 100 ;10*a) 5
let res2= doRequestSync processor (fun x->printfn "mulby5 delay";delay 1000; 5*x) 2
let res3=doRequestSync processor (fun a-> printfn "mulby10 delay2 "; delay 100 ;10*a) 5