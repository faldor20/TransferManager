namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
open System.Linq
open TransferClient


///contains one schedule id for each job in each jobsource.
/// source a=[job1,job2] b=[job1] jobOrder=[(a);(b);(a)]
type JobOrder= (ScheduleID)ResizeArray

module JobOrder =
    ///Counts the number of ScheduleID's of the same time in the jobid before the one given
    let countBefore (jobOrder:JobOrder) countItem index=
            let mutable amount=0
            for i in [0..index-1] do
                if jobOrder.[i]=countItem then amount<-amount+1
            amount
    ///Returns each scheduleid along with its count in the list
    /// eg [a,0;b,0;b,1;c,0]
    let countUp (jobOrder:IEnumerable<'a>)=
        let counts=Dictionary<'a,int>()
        jobOrder.Select(fun job->
            if not (counts.ContainsKey(job)) then counts.[job]<-0
            else counts.[job]<-counts.[job]+1
            (job,counts.[job])
        ).ToArray()

   ///Returns a list of jobs from the joborder that have all their required tokens and are avilable
    ///Removes the jobs from the JobOrder and Source 
    let takeAvailableJobs (jobOrder:JobOrder) (sources:SourceList)=
        let indexed=countUp jobOrder
        let jobsToRun=
            seq{
            for (jobSource,i) in indexed do
                let job=sources.[jobSource].Jobs.[i]
                if job.TakenTokens = sources.[jobSource].RequiredTokens && job.Available then
                    yield (jobSource,job.ID,i)
            }|>Seq.toList
        
        //Removes each job from the joborder and its source
        jobsToRun|>List.map(fun (source,id,index)->
            match jobOrder.Remove(source) with
            |true->()
            |false->Logging.errorf "Tried to remove a job that should have been there but wasn't"
            //this removes the job from the source list
            sources.[source].Jobs.RemoveAt(index)
            (source,id)
            )

        
 