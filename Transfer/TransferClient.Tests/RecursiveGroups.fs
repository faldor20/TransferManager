module RecursiveGroups

open TransferClient.IO.Types
open TransferClient.JobManager.Types
open Expecto
open System.Collections.Generic
open TransferClient.JobManager.JobRunner

let tupleToKeyValue inp =
    let fst, snd = inp
    KeyValuePair(fst, snd)

let makeGroupDic input =
    new Dictionary<string, Group<string>>(input |> List.map tupleToKeyValue)

let makeGroup nextGroup joblist (scheduleTokens: string list) =
    { NextGroup = nextGroup
      JobList = joblist
      ScheduleTokens = new List<string>(scheduleTokens) }

let makeJob tokens job = { Job = job; ScheduleTokens = tokens }
let makeNewJob a= makeJob[""] a 
let gentestDic() =
    makeGroupDic
        [ "Remote",
          (makeGroup
              (Some(makeGroupDic [ "Bun", makeGroup None [ makeNewJob "Bunjob2";makeNewJob "Bunjob1"] [ "Bun" ] ;"Rky", makeGroup None [makeNewJob"RkyJob2"] ["Rky"]]))
               [ makeJob  ["Rky"] "Rkyjob1"]
               [ "Bun" ]) ]
let genTestGroup()=
    makeGroup
        (Some <|gentestDic())
        [makeJob ["Remote";"Bun"]"Bunjob1"]
        [""]
let makeRecDic inp =
    let outp =new Dictionary<'a,RecDict<'a,'b>>(inp|>List.map tupleToKeyValue),None
    Middle outp
let makeRecDicEnd inp =
    let outp ={MutDat=inp}
    End outp
let genRecDic ()=
    makeRecDic["top a",makeRecDic["end a",makeRecDicEnd ["end a 1";"end a 2"]] ;"top b",makeRecDic["end b",makeRecDicEnd ["end b 1";"end b 2"]]]
[<Tests>]
let tests =
    testSequenced
    <| testList "Job Scheduler Tests" [ 
        test "Can add job to jobList" {
            let testDic= genTestGroup()
            let position=["Remote";"Bun"]
            let newJob=(makeJob  [""] "BunJob4")
            addNewJob (testDic) newJob  position
            let changedList=testDic.NextGroup.Value.[position.[0]].NextGroup.Value.[position.[1]].JobList
            Expect.contains changedList newJob "joblist contains new job" 
          };
        test "{EditRec}Can edit data in recData " {
            let testDic=genRecDic()
            let position=["top a";"end a"]
            let newEnd=["ho ho ho"]
            Utils.logInfof "{EditRec}originalData=%A " testDic 
            let res= setData testDic ["top a";"end a"] ["ho ho ho"]
            let changedList= 
                match drillToData testDic position with
                |Ok data->
                    data
                |Error error->
                    {MutDat=[""]}
            Expect.isOk res "result okay"
            Utils.logInfof "{EditRec}NewData=%A changedvar=%A" testDic changedList
            Expect.equal changedList.MutDat newEnd "joblist contains new job" 
          };
          test "{EditRec}Can edit drilled data in recData " {
            let testDic=genRecDic()
            let position=["top a";"end a"]
            let newEnd=["ho ho ho"]
            Utils.logInfof "{EditRec}originalData=%A " testDic 
            let res= drillToData testDic ["top a";"end a"]
            let outt=
                match res with
                |Ok data-> data.MutDat<-newEnd
                |Error message->Expect.equal message "" "Failed getting data first time"
            let changedList= 
                match drillToData testDic position with
                |Ok data->
                    data
                |Error error->
                    {MutDat=[""]}
            Expect.isOk res "result okay"
            Utils.logInfof "{EditRec}NewData=%A changedvar=%A" testDic changedList
            Expect.equal changedList.MutDat newEnd "joblist contains new job" 
          } ]
