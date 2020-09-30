namespace TransferClient.DataBase
open System.Collections.Generic
open System.Threading
open TransferClient.JobManager
open SharedFs.SharedTypes
module TokenDatabase=
    let mutable CancellationTokens=new Dictionary<JobID,CancellationTokenSource> ()
    let addCancellationToken id token=
        lock CancellationTokens (fun()->
            if not( CancellationTokens.ContainsKey id) then
                TransferClient.Logging.debugf "Cancellation token DB doesn't contain id: %A adding it now" id
            CancellationTokens.Add(id,token)
        )
    let cancelToken id =
        CancellationTokens.[id].Cancel()