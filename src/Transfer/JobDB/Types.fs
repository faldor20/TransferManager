namespace JobManager
open SharedFs.SharedTypes
open Mover.Types
open System.Threading


type Job = Async<TransferResult*bool>

type JobItem =
    { Job: Job
      SourceID:SourceID
      ID:JobID
      mutable Available:bool
      CancelToken:CancellationTokenSource
      mutable TakenTokens: SourceID list }
 ///A list of SourceIDs represetnig a specific place within the jobHeirachy with the highest level id at the beginning and the deepest at the end
type HierarchyLocation = SourceID list
///required tokens is last reuired to firstrequired
/// Rules:
/// A job with a lower number of tokens will allways sit below one with a higher number
/// Jobs will allways be removed from the list in the order they were added(assuming no user swap requests)
type Source= {Jobs:ResizeArray<JobItem>;RequiredTokens:SourceID list}
module Locking=
    let lockedFunc locObj func=
        lock(locObj) (fun()->func )

