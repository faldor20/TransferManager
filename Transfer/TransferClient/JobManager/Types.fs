namespace TransferClient.JobManager
open SharedFs.SharedTypes
open TransferClient.IO.Types


type Job = Async<TransferResult*bool>

type JobItem =
    { Job: Job
      SourceID:ScheduleID
      ID:JobID
      mutable Available:bool
      mutable TakenTokens: ScheduleID list }
 ///A list of ScheduleIDs represetnig a specific place within the jobHeirachy with the highest level id at the beginning and the deepest at the end
type HierarchyLocation = ScheduleID list
///required tokens is last reuired to firstrequired
/// Rules:
/// A job with a lower number of tokens will allways sit below one with a higher number
/// Jobs will allways be removed from the list in the order they were added(assuming no user swap requests)
type Source= {Jobs:ResizeArray<JobItem>;RequiredTokens:ScheduleID list}
module Locking=
    let lockedFunc locObj func=
        lock(locObj) (fun()->func )

