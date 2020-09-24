namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
type TokenStore={
    Token:ScheduleID;
    mutable Remaining:int;
    mutable SourceOrder: ScheduleID list 
}
type TokenList = Dictionary<ScheduleID, TokenStore>
module TokenList =
        ///Decriments the tokenSources Remaing number if possible. returns a token if it was and None if it wasn't
        let  takeToken'  tokenSource=
            lock tokenSource (fun ()->
                let newTokens, outp =
                    match tokenSource.Remaining with
                    | a when a > 0 -> tokenSource.Remaining - 1, Some tokenSource.Token
                    | a when a = 0 -> 0, None
                    | a when a < 0 -> failwithf "FreeTokens for tokenId %A under 0 this should never ever happen" id

                tokenSource.Remaining <- newTokens
                outp)
        ///Decriments the tokenSources Remaing number if possible. returns a token if it was and None if it wasn't
        let takeToken  id (tokenDB: TokenList)=
            takeToken' tokenDB.[id]
        ///Increases the remaing count by one
        let returnToken (tokenDB: TokenList) id = tokenDB.[id].Remaining <- tokenDB.[id].Remaining + 1
        let AddTokenSource (tokenDB: TokenList) id tokenSource = tokenDB.[id] <- tokenSource
   