namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
open TransferClient


type SourceList= Dictionary<SourceID,Source>

module SourceList =
    //this is fair but we actually don't want fairness we want jobs to be issued d=tokens in the order defined by the JobOrder.
    
    ///Attempts to give all the tokens in the tokenStore away to any jobs that want them. 
    /// Will iterate through every source provided and evey job within a source that desires the token in the tokenStore. Will probably be quite an expensive procedure
    let rec attmeptIssuingToken  (sources: SourceList) (tokenSource:TokenStore)=
        ///This loop iterates jobs untill one is found that the token can be inserted into. At which point it returns true. if none is found it returns false
        let rec jobLoop (source:Source) ID i=
            if i=source.Jobs.Count then false 
            else
                let job= source.Jobs.[i]
                let wantedTokens=source.RequiredTokens|>List.except job.TakenTokens
                match wantedTokens with 
                |[]-> jobLoop source ID (i+1)
                |tokens->
                    let nextToken=wantedTokens|>List.last
                    if nextToken= tokenSource.Token then
                        match TokenList.takeToken' tokenSource with
                        |Some token->
                            job.TakenTokens<- token::job.TakenTokens
                            //This removes our item and then adds it at the end
                            tokenSource.SourceOrder <-(tokenSource.SourceOrder|>List.except[ID])@[ID]
                            //we then run it again just incase there are some tokens left
                            true
                        |None->
                            Logging.errorf "Something has gone wrong an attempt was made to issue a token but it failed"
                            true
                    else jobLoop source ID (i+1)
        ///loops until the jobloop returns true, then runs the main function again. this is incase multiple tokens were added at once
        let rec iter (sourceIDs)=
            match sourceIDs with
            | head::tail-> 
                if jobLoop sources.[head] head  0 then tokenSource|>attmeptIssuingToken  sources
                else iter tail
            |[]->()
        if tokenSource.Remaining>0 then
            iter tokenSource.SourceOrder
    
    ///this will attempt to get the given job the tokens it needs, in order. it will run recursivley untill a token cannot be had or it has all that it needs
    let rec getNextToken (freeTokens:TokenList)  (source:Source) (job:JobItem) =
        Logging.debugf "{SourceList} Getting Next token for job %i. Job has tokens: %A. Needs: %A"job.ID job.TakenTokens source.RequiredTokens
        if job.TakenTokens.Length =source.RequiredTokens.Length then
            ()
        else
        //TODO:this is done very very often and so it could be made faster by precomputing the requiredToken.
            let neededToken=
                source.RequiredTokens
                |>List.except job.TakenTokens
                |>List.last
            match freeTokens|> TokenList.takeToken neededToken with
            |Some token-> 
                job.TakenTokens<- token::job.TakenTokens
                getNextToken freeTokens source job
            |None->()
    ///Shold be run at regular intervals to give jobs the tokens they need to be picked up by the runner and run 
    /// this makes ordering fair because it attemps to give tokens in the same alternating order as the jobqueue
    let updateTokens  (freeTokens:TokenList)  (sources:SourceList) =
        sources|>Seq.iter (fun source->
            for job in source.Value.Jobs do
                getNextToken freeTokens source.Value job
        )
   