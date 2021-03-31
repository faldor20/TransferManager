module Program = 
    open Expecto
    let [<EntryPoint>] main args = 
        runTestsInAssemblyWithCLIArgs [] args
