namespace TransferClient
open System.Collections.Generic
open System.Threading
module TokenDatabase=
    let mutable CancellationTokens=new Dictionary<string,Dictionary<int,CancellationTokenSource> >()
    let addCancellationToken groupName id token=
        lock CancellationTokens (fun()->
            if not( CancellationTokens.ContainsKey groupName) then
                let res=CancellationTokens.TryAdd(groupName, new Dictionary<int,CancellationTokenSource>() )
                if not res then printfn"[ERROR]Something went wrong creating token list for %s " groupName
            CancellationTokens.[groupName].Add(id,token)
        )