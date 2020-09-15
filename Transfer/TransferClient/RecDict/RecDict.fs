namespace TransferClient
open SharedFs.SharedTypes

open System
open FSharp.Control.Reactive

open TransferClient.IO.Types
open FSharp.Control
open Result
open DataBase.Types
open System.Collections.Generic

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
type Keys=
|KeyMid of (list<Keys>*string)
|KeyEnd of string
type MoveJob= Async<TransferResult*TransDataAcessFuncs*bool>

module RecDict=
    let makeKeys (groups: list<list<string>>)=
        let group (a:list<list<string>>)=
            a|>List.groupBy (fun  x ->
                match x with
                |head::tail->head
                |[]->""
                    ) 
        let rec unwrap (a:list<list<string>>)=
                group a|>List.map(fun x->
                match x with
                |y,[head] when head.Length=1-> KeyEnd head.[0]
                |y,list->
                    let trimmedLists=(list|>List.map(List.tail))
                    KeyMid ((unwrap trimmedLists),y ))
        match unwrap groups with
        |[head]-> Ok head
        |_->Error "Given a groups list with more than one top level group. All group lists must being with the same group. \n Never: ['top','mid','end'],['othertop','mid','end']"

    let tupleToKeyValue inp =
        let fst, snd = inp
        KeyValuePair(fst, snd)
    let emptyMid ()=
        let outp =
            new Dictionary<'a, RecDict<'a, 'b, 'c>>(), new 'b()

        Middle outp
    let makeRecDicMiddle inp data=
        let outp =
            new Dictionary<'a, RecDict<'a, 'b, 'c>>(inp |> List.map tupleToKeyValue), data

        Middle outp

    let makeRecDicEndMut inp =
        let outp = { MutDat = inp }
        End outp
    let makeRecDicEnd inp =
        End inp

    /// takes a recursive key list 
    let rec emptyMut (keys:Keys) emptyData=
        match keys with
        |KeyMid (next, key)-> 
            let inp=(next|>List.map(fun x-> key,emptyMut x emptyData))
            makeRecDicMiddle inp  {MutDat =emptyData}
        |KeyEnd(key)-> makeRecDicMiddle [ key,makeRecDicEnd emptyData] {MutDat =emptyData}

    let rec empty (keys:Keys) emptyMid emptyEnd=
        match keys with
        |KeyMid (next, key)-> 
            let inp=(next|>List.map(fun x-> key,empty x emptyMid emptyEnd))
            makeRecDicMiddle inp  emptyMid
        |KeyEnd(key)-> makeRecDicMiddle [ key,(End emptyEnd)] emptyMid
        
        

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
    let rec drillToData (recDict:RecDict<'a,'b,'c>) keys =
         match recDict with
        |Middle (next,data)->
             match keys with
             |[]->Ok (MiddleType data)
             |head::tail->
                try
                drillToData next.[head] tail
                with ex-> Error <|sprintf "failed with error: %A"ex
        |End data->
            match keys with
            |[]->Ok (EndType data)
            |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)

    ///like drill to data but creates any non existant levesl as it moves down the heiache
    let rec drillToDataSafe (recDict:RecDict<'a,'b,'c>) keys =
        match recDict with
        |Middle (next,data)->
             match keys with
             |[]->Ok (MiddleType data)
             |head::tail->
                
                if not(next.ContainsKey head) then 
                    if tail.Length>0 then next.Add(head, emptyMid())
                    else  next.Add(head, End (new 'c()))
                try
                drillToDataSafe next.[head] tail
                with ex-> Error <|sprintf "failed with error: %A"ex
        |End data->
            match keys with
            |[]->Ok (EndType data)
            |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)
    ///Just like drillTodata but doesnt return a n either type because the two data types stored in the RecDict must be the same
    let rec drillToSameData (recDict:RecDict<'a,'b,'b>)safe keys =
        (match safe with 
        |false-> drillToData
        |true->drillToDataSafe
        )recDict keys |>Result.bind(fun x->
        Ok (match x with |MiddleType y->y|EndType y->y))
        
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
                    Ok ()
                |head::tail->Error (sprintf "Reached end of Dictionary heirachey before end of keys. Ran out at: %A Remaining:%A " head tail)
        intern recDict keys
    type GroupDic<'a>=
        |Dic of Dictionary<string,GroupDic<'a>>
        |Job of 'a 
