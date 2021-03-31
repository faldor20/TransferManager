module TransferClient.HierarychGenerator
open System.Collections.Generic
open System.Linq

///this ungodly monstrosity transforms the input into a list that has the distinct items from every layer of the groups
///The heirachy is as follows:
///In the input each list is a single leg of the heirachy
///in the output heach list is a single level
///       [ a                     [ a  ] list1      
///       /  \                    /  \     
///      b    c                [ b    c ] list2    
///    /  \    \               /  \    \   
///   k    g    d ] list1   [ k    g    d ] list3  
///eg: [ [a,b,k],[a,c,d],[a,b,g] ] becomes [ [a],[b,c],[d,g,k] ]
let private transformGroups groups =
    groups
    |> List.collect List.indexed
    |> List.groupBy (fun (index, y) -> index)
    |> List.map (fun (_, y) -> (y |> List.map (fun (_, y) -> y) |> List.distinct))

//For each group there is now a key in a dictionary whos value is the groups that are below that group in the heirachy
///eg: [[a,b,k],[a,c,d],[a,b,g]] becomes [ {a:[b,c]} ,{b:[k,g] c:[d]}]
let makeHeirachy (groups: int list list) =
    let longestGroup =
        groups|> List.fold (fun y x -> if y < x.Length then x.Length else y) 0

    let output: Dictionary<int, List<int>> list =
        List.init longestGroup (fun x -> Dictionary())

    //initialise all the lists
    let distinctGroups = transformGroups groups
    (output, distinctGroups)
    ||> List.iter2 (fun out lis ->
            lis
            |> List.iter (fun x -> out.[x] <- new List<int>()))
    //give each group its children in the heirachy
    groups
    |> List.iter (fun x ->
        x
        |> List.pairwise
        |> List.iteri (fun i (x1, x2) -> 
        output.[i].[x1].Add(x2)))
    //we may have duplicate entries in the heirachy so we should distinctify it
    let out=
        output
        |>List.map (fun  dic-> 
            dic
            |>(Seq.map(fun entry ->
                KeyValuePair(entry.Key, dic.[entry.Key].Distinct().ToList())))
                |>Dictionary 
                )

    out|>List.toArray