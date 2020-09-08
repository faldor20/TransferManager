namespace TransferClient.DataBase
open System.Collections.Generic
open System.Threading
module TokenDatabase=
    let mutable CancellationTokens=new Dictionary<string,Dictionary<int,CancellationTokenSource> >()
    let addCancellationToken groupName id token=
        lock CancellationTokens (fun()->
            if not( CancellationTokens.ContainsKey groupName) then
                TransferClient.Logging.debugf "Cancellation token DB doesn't contain group: %s adding it now" groupName
                let res=CancellationTokens.TryAdd(groupName, new Dictionary<int,CancellationTokenSource>() )
                if not res then TransferClient.Logging.errorf"{TokenDB} Something went wrong creating token list for %s " groupName
            CancellationTokens.[groupName].Add(id,token)
        )
    let cancelToken groupName id =
        CancellationTokens.[groupName].[id].Cancel()