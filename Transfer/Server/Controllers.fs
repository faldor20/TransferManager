
namespace Transfer.Server
open Giraffe.GiraffeViewEngine
open Giraffe

module Controllers=
    let helloAction name=
        htmlView(Views.hello name)
    

    //let copyAction =(htmlView (Views.Copies ((Transfer.Data.getAsSeq|>Seq.map(fun (a,b)->b)) |> Seq.toList ) ))


  