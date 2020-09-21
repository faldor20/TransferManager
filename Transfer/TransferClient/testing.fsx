(* open System.IO
File.Delete("./testSource/nixos-minimal-19.09.1019.c5aabb0d603-x86_64-linux.iso") *)
let a groups=
    groups
    |> List.map List.indexed
    |> List.concat
    |> List.groupBy (fun (x, y) -> x)
    |> List.map (fun (_, y) ->  (y |> List.map (fun (_, y) -> y) |> List.distinct))
let lis= [["a";"b";"x";"k"];["a";"c";"y"];["a";"d"];["a";"c";"y";"p"]]
a lis
