(* open System.IO
File.Delete("./testSource/nixos-minimal-19.09.1019.c5aabb0d603-x86_64-linux.iso") *)
open System
open System.Linq
open System.Collections.Generic
(* let countUp (jobOrder:IEnumerable<'a>)=
    let counts=Dictionary<'a,int>()
    jobOrder.Select(fun job->
        if not (counts.ContainsKey(job)) then counts.[job]<-0
        else counts.[job]<-counts.[job]+1
        (job,counts.[job])
    ).ToArray()
let lis= ["a";"b";"x";"k";"a";"c";"y";"a";"d";"a";"c";"y";"p"]
countUp lis *)
//Test to see if an instance of FIleInfo is upadted if file changes
//The answer is no. it does not change.
open System.IO
try
    Directory.CreateDirectory("./testDir/")
    let testFilePath= "./testDir/testFile"
    File.WriteAllText(testFilePath,"hi")
    let firstInfo= FileInfo(testFilePath)
    printfn "firstlength1= %i" firstInfo.Length
    File.AppendAllText(testFilePath," heyyyy")
    printfn "firstlength2= %i" firstInfo.Length
    let secondInfo= FileInfo(testFilePath)
    printfn "secondlength1= %i" secondInfo.Length
    printfn "firstlength3= %i" firstInfo.Length
finally 
    Directory.Delete("./testDir",true)