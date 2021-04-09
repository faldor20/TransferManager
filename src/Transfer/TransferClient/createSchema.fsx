#r "nuget: FSharp.Data.JsonSchema, 1.0.0"
#r "nuget: NJsonSchema, 10.4.0"
#r "nuget: Thoth.Json.Net, 5.0.0"
#r @"C:\Users\***REMOVED***\.nuget\packages\fluentftp\33.0.3\lib\netstandard2.1\FluentFTP.dll"
#r @"c:\Users\***REMOVED***\Desktop\Code\FSharp\fs\src\Transfer\LoggingFsharp\bin\Debug\net5.0\LoggingFsharp.dll"
#r @"C:\Users\***REMOVED***\.nuget\packages\legivel\0.4.5\lib\netstandard2.0\Legivel.Mapper.dll"
#r @"C:\Users\***REMOVED***\.nuget\packages\legivel\0.4.5\lib\netstandard2.0\Legivel.Parser.dll"
#r @"c:\Users\***REMOVED***\Desktop\Code\FSharp\fs\src\Transfer\Mover\bin\Debug\net5.0\Mover.dll"
#r @"c:\Users\***REMOVED***\Desktop\Code\FSharp\fs\src\Shared\bin\Debug\netstandard2.0\SharedData.dll"
open FSharp.Data.JsonSchema
open Thoth.Json.Net
open Mover.Types
#load "./ConfigReader.fs"
open TransferClient.ConfigReader
open System.IO
type Apple={
    Seeds:int
    Bitten:bool
}
type Food=
|Apple of Apple
|Notapple 
type Inner={
    Inner1:int
    Inner2:string
}
type Outer={
    Outer1:Inner option
    Food:Food
}
let gen=FSharp.Data.JsonSchema.Generator.CreateMemoized ("out")

let outer= {Outer1=Some{Inner1=1;Inner2="this is text";}; Food=Apple{Seeds=10;Bitten=true;};}
File.WriteAllText("./thoth.json",
 Encode.Auto.toString(4,outer)
)
let outer2= Decode.Auto.fromString<Outer>(File.ReadAllText("./thoth.json"))
match outer2 with
|Ok(res)->printfn "%A" res
|Error(error)->printfn "error:%s" error
let scheme=NJsonSchema.JsonSchema.FromType(typeof<Outer>)
File.WriteAllText("./njson.json",scheme.ToJson())

 

let a= gen (typeof<Inner>)
let b=gen (typeof<Outer>)
System.IO.File.WriteAllText("Schema.json",b.ToJson());
System.IO.File.WriteAllText("Schema2.json",a.ToJson()); 
