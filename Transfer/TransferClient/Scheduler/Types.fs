namespace TransferClient.JobManager
open SharedFs.SharedTypes

open System
open FSharp.Control.Reactive

open TransferClient.DataBase
open FSharp.Control
open TransferClient.IO.Types
open TransferClient.DataBase.Types
open Result
open System.Collections.Generic
module Types=
    ///This type exists entirley to enable the RecDict DU to have its single type mutated
    type MutableData<'b>=
        {mutable MutDat:'b}
    ///if you would like the data parts to be mutable wrap your data in a "MutableData" record
    type RecDict<'a,'b,'c>=
        |End of 'c
        |Middle of (Dictionary<'a,(RecDict<'a,'b,'c>)>*'b )
    type Either<'b,'c>=
    |EndType of 'c
    |MiddleType of 'b
    type MoveJob= Async<TransferResult*TransDataAcessFuncs*bool>

    let rec drill (recDict:RecDict<'a,'b,'c>) key=
        
        match recDict with
        |Middle (thisDic,data)->
            try 
                Ok thisDic.[key]
            with|exc->Error <|sprintf"acess faield with error: %A" exc
            
        |End data->
            Error "Allready at end"
    ///Gets the data from a point in the recursive dictionary.
    /// Returns an Either<'b,'c> type that represents either an end datatype or a middle datatype 
    /// if the data is a MutableData record the refence you recieve back will effect the dic passed in 
    let rec drillToData (recDict:RecDict<'a,'b,'c>) keys=
        
        match recDict with
        |Middle (thisDic,data)->
             match keys with
             |[]->Ok (MiddleType data)
             |head::tail->
                let nextDic= thisDic.[head]
                drillToData nextDic tail
        |End data->
            match keys with
            |[]->Ok (EndType data)
            |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)
    ///Just like drill to data but fails if the key doesn't get all the way to the end of the heirachy and only returns the end dta type "'c"
    let rec drillToEndData (recDict:RecDict<'a,'b,'c>) keys=
        match recDict with
        |Middle (thisDic,_)->
             match keys with
             |[]->Error "keys ran out before reaching end of heirachy"
             |head::tail->
                let nextDic= thisDic.[head]
                drillToEndData nextDic tail
        |End data->
            match keys with
            |[]->Ok data
            |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)
    
    let setData (recDict:RecDict<'a,MutableData<'b>,MutableData<'b>>) keys input=
        let rec  intern dic keysRem=
            match dic with
            |Middle (thisDic,_)->
                 match keysRem with
                 |[]->Error ""
                 |head::tail->
                    let nextDic= thisDic.[head]
                    intern nextDic tail
            |End data->
                match keysRem with
                |[]->
                    data.MutDat<-input
                    Ok true
                |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)
        intern recDict keys
    type GroupDic<'a>=
        |Dic of Dictionary<string,GroupDic<'a>>
        |Job of 'a 
