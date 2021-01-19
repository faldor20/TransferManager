namespace TransferClient.JobManager

open System
open System.Collections.Generic
open SharedFs.SharedTypes
open TransferClient.IO.Types
open SharedFs.SharedTypes
open TransferClient
module Main =


 
    open System.Linq
 
    ///SoureOrder is a list of the sources that want the token. each time a token is issued the issuing sources id is put at the end
   
    type RunningJobs= List<JobID>
    type FinishedJobs=List<JobID>
    type JobDataBase={
        JobOrder:JobOrder;
        mutable FreeTokens:TokenList
        RunningJobs:RunningJobs
        FinishedJobs:FinishedJobs
        mutable Sources:SourceList
        TransferDataList:TransferDataList
        JobList:JobList
        mutable RunJob:SourceID->JobID->Async<unit>
        SyncEvents: Syncer.SyncEvents
        
    }
    let JobDataBase runJob mapping heirachy={
        JobOrder=JobOrder();
        FreeTokens=TokenList()
        RunningJobs=RunningJobs()
        FinishedJobs=FinishedJobs()
        Sources=SourceList()
        TransferDataList=TransferDataList()
        JobList=JobList()
        RunJob=runJob
        SyncEvents=Syncer.SyncEvents()
    }