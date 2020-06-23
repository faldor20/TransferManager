
namespace Transfer.Server
module Routers=

    open Saturn
    open Giraffe.Core
    open Giraffe.ResponseWriters
    open Controllers
    open FSharp.Control.Tasks
    let browser = pipeline {
        plug acceptHtml
        plug putSecureBrowserHeaders
        plug fetchSession
        set_header "x-pipeline-type" "Browser"
    }

    let defaultView = router {
        get "/" (htmlFile "./index.html" )
        get "/index.html" (redirectTo false "/")
        get "/default.html" (redirectTo false "/")
    }
    let helloView = router {
        getf "/%s" helloAction
    }
    let copyView = router {
        get "/" (htmlView (Views.Copies ((Transfer.Data.getAsSeq|>Seq.map(fun (a,b)->b)) |> Seq.toList ) ))
    }
    let api= router{
        get "/" (json"this is an api")
        get "/transferdata" (fun next ctx ->
            task {
                return! json Transfer.Data.data next ctx
            })
    }

    let browserRouter = router {
        forward "/api" api
       // not_found_handler(htmlView Views.notFound )//Use the default 404 webpage
       // pipe_through browser //Use the default browser pipeline

      //  forward "/_" defaultView //Use the default view
       // forward "" defaultView //Use the default view
       // forward "" defaultView //Use the default view
       // forward "" defaultView //Use the default view
       // forward "/hello" helloView
      //  forward "/copyprogress" copyView

       
    }

    //Other scopes may use different pipelines and error handlers

    // let api = pipeline {
    //     plug acceptJson
    //     set_header "x-pipeline-type" "Api"
    // }

    // let apiRouter = router {
    //     not_found_handler (setStatusCode 404 >=> text "Api 404")
    //     pipe_through api
    //
    //     forward "/someApi" someScopeOrController
    // }

    let appRouter = router {
        // forward "/api" apiRouter
        forward "/api" api //for some reason this must be here for the return
        forward "" browserRouter
    }