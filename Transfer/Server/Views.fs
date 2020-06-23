
namespace Transfer.Server
open Giraffe.GiraffeViewEngine
open Giraffe

open Transfer.Data
open System.Collections.Generic
open SharedData;
module ViewGenerators=
    let generateTask (data:TransferData)=
        div[][
            h2[][rawText (data.Source+" -> "+data.Destination )]
            div[][rawText ("progress:"+string data.Percentage+"%")]
            div[][rawText ("speed:"+string data.Speed+"MB/s")]
        ]

module Views=

    let index =
        div[][
            h2[][rawText"Hello"]
        ]
    let notFound =
        div[][
            h2[][rawText"404"]
        ]
    let hello  name =
        div[][
            h2[][rawText("hello" + name + "!")]
        ]

    
    let Copies (copyJobs:TransferData list)=
        div[][
            ul[][
                yield!
                    copyJobs|> List.map(fun job -> li [] [ (ViewGenerators.generateTask job) ])
            ]
        ]
