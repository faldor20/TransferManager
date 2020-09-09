module RecursiveGroups

open TransferClient.IO.Types
open TransferClient.JobManager.Types
open Expecto
open System.Text.Json
open System.Collections.Generic
open TransferClient.JobManager.JobRunner
open System.Text.Json.Serialization

let tupleToKeyValue inp =
    let fst, snd = inp
    KeyValuePair(fst, snd)

let makeGroupDic input =
    new Dictionary<string, Group<string>>(input |> List.map tupleToKeyValue)

let makeGroup nextGroup joblist (scheduleTokens: string list) =
    { NextGroup = nextGroup
      JobList = joblist
      ScheduleTokens = new List<string>(scheduleTokens) }

let makeJob tokens job =
    { Job = job
      TakenScheduleTokens = tokens }

let makeNewJob a = makeJob [  ] a

let gentestDic () =
    makeGroupDic
        [ "Remote",
          (makeGroup
              (Some
                  (makeGroupDic [ "Bun",
                                  makeGroup
                                      None
                                      [ makeNewJob "Bunjob2"
                                        makeNewJob "Bunjob1" ]
                                      [ "Bun" ]
                                  "Rky", makeGroup None [ makeNewJob "RkyJob2" ] [ "Rky" ] ]))
               [ makeJob [ "Rky" ] "Rkyjob1" ]
               [ "Bun" ]) ]

let genTestGroup () =
    makeGroup (Some <| gentestDic ()) [ makeJob [ "Remote"; "Bun" ] "Bunjob1" ] [ "" ]

let makeRecDic inp data=
    let outp =
        new Dictionary<'a, RecDict<'a, 'b, 'c>>(inp |> List.map tupleToKeyValue), data

    Middle outp

let makeRecDicEnd inp =
    let outp = { MutDat = inp }
    End outp

let makeLevel jobList tokens=
    {JobList=jobList;AvailableScheduleTokens=tokens}
let genRecDic () =
    makeRecDic [ "top a", makeRecDic [ "end a", makeRecDicEnd [ "end a 1"; "end a 2" ] ] { MutDat = [  ] }
                 "top b", makeRecDic [ "end b", makeRecDicEnd [ "end b 1"; "end b 2" ] ] { MutDat = [  ] } ] 
                { MutDat = [  ] }
let genJobDic () =
    makeRecDic  ["Global",(makeRecDic [
                                ("Remote", makeRecDic [ "Bun", makeRecDicEnd [makeNewJob "BunJob2" ;makeNewJob "BunJob3"] ; "Rky", makeRecDicEnd [makeNewJob "RkyJob1" ;makeNewJob "RkyJob2"] ]<| (makeLevel [ "BunJOb1"|>makeJob ["Bun"]]["Bun";"Rky"]))
                                ("Local", makeRecDic [ "ToQuantel", makeRecDicEnd [makeNewJob "ToQuantelJob2" ;makeNewJob "ToQuantelJob3"] ]<|(makeLevel [ "ToQauntelJob1"|>makeJob ["ToQuantel"]]["ToQauntel"]))
                                ] <|(makeLevel [ "ToQauntelJob0"|>makeJob ["ToQuantel";"Local"]]["Remote";"Local";"Local";"Local"]))
                ]<|(makeLevel[makeJob ["Global";"Remote";"Bun"] "BunJob0"]["Global";"Global";"Global";"Global"])

[<Tests>]
let tests =
    testSequenced
    <| testList
        "Job Scheduler Tests"
           [ test "Can add job to jobList" {
                 let testDic = genTestGroup ()
                 let position = [ "Remote"; "Bun" ]
                 let newJob = (makeJob [ "" ] "BunJob4")
                 addNewJob (testDic) newJob position

                 let changedList =
                     testDic.NextGroup.Value.[position.[0]].NextGroup.Value.[position.[1]].JobList

                 Expect.contains changedList newJob "joblist contains new job"
             }
             test "{EditRec}Can edit data in recData " {
                 let testDic = genRecDic ()
                 let position = [ "top a"; "end a" ]
                 let newEnd = [ "ho ho ho" ]
                 Utils.logInfof "{EditRec}originalData=%A " testDic

                 let res =
                     setData testDic [ "top a"; "end a" ] [ "ho ho ho" ]

                 let changedList =
                     match drillToData testDic position with
                     | Ok data -> 
                        (match data with 
                        |MiddleType mid->mid
                        |EndType en->en
                        )
                     | Error error -> { MutDat = [ "" ] }

                 Expect.isOk res "result okay"
                 Utils.logInfof "{EditRec}NewData=%A changedvar=%A" testDic changedList
                 Expect.equal changedList.MutDat newEnd "joblist contains new job"
             }
             test "{EditRec}Can edit drilled data in recData " {
                 let testDic = genRecDic ()
                 let position = [ "top a"; "end a" ]
                 let newEnd = [ "ho ho ho" ]
                 Utils.logInfof "{EditRec}originalData=%A " testDic
                 let res = drillToData testDic [ "top a"; "end a" ]

                 let outt =
                     match res with
                     | Ok data -> 
                        (match data with 
                        |MiddleType mid->mid
                        |EndType en->en
                        ).MutDat <- newEnd
                     | Error message -> Expect.equal message "" "Failed getting data first time"

                 let changedList =
                     match drillToData testDic position with
                     | Ok data -> 
                        (match data with 
                        |MiddleType mid->mid
                        |EndType en->en
                        )
                     | Error error -> { MutDat = [ "" ] }

                 Expect.isOk res "result okay"
                 Utils.logInfof "{EditRec}NewData=%A changedvar=%A" testDic changedList
                 Expect.equal changedList.MutDat newEnd "joblist contains new job"
             }
             test "{EditRec} Moveup works " {
                let testDic = genJobDic ()
                let beforeChange= genJobDic()
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                Utils.logInfof "{EditRec}Before shuffel=\n %s " (JsonSerializer.Serialize( beforeChange,options))
                shuffleUp2 testDic
                Utils.logInfof "{EditRec}After shuffel= \n %s " (JsonSerializer.Serialize( testDic,options))
                Expect.notEqual testDic beforeChange "joblist has changed"
                let (Middle (before,bData))=  beforeChange
                let (Middle (after,aData)) = testDic
                Expect.isLessThan aData.AvailableScheduleTokens.Length bData.AvailableScheduleTokens.Length "less availabel tokens in database"
                Expect.isGreaterThan aData.JobList.Length bData.JobList.Length "More jobs in top level joblist"
                
             }
             test "{EditRec} Schedule Tokens are returned  " {
                let testDic = genJobDic ()
                let beforeChange= genJobDic()
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                Utils.logInfof "{EditRec}Before Return=\n %s " (JsonSerializer.Serialize( beforeChange,options))
                let schedulesToReturn=["Global";"Remote";"Bun"] 
                let res= returnScheduleTokens'' testDic schedulesToReturn
                
                Utils.logInfof "{EditRec}After Return= \n %s " (JsonSerializer.Serialize( testDic,options))
                Expect.notEqual testDic beforeChange "DB has changed"
                Expect.isOk res "Didn't return failure"
                let (Middle (before,bData))=  beforeChange
                let (Middle (after,aData)) = testDic
                Expect.isGreaterThan aData.AvailableScheduleTokens.Length bData.AvailableScheduleTokens.Length "MoreAvailableTokens"
                let rec testAll (before:string list)(after:string list)=
                    let newBefore=before|>List.take (before.Length-1)
                    let newAfter=after|>List.take (after.Length-1)
                    let test li=
                        match drillToData testDic ( li)with
                        |Ok o->
                            match o with 
                            |MiddleType a-> a.AvailableScheduleTokens.Length
                    Expect.equal ((test newBefore)+ 1) (test newAfter) "AvaliableSchedulelist increased in size by one"
                    if(newAfter.Length>0) then testAll newBefore newAfter
                testAll schedulesToReturn

               
                
             } ]
