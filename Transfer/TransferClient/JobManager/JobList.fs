﻿namespace TransferClient.JobManager
open System.Collections.Generic
open SharedFs.SharedTypes
open Locking
type JobList = Dictionary<JobID, JobItem>

module JobList =
    ///Adds a job to the list. mkaeJob is  function that takes in an int, the jobItems id and returns a jobItem.
    /// this allows for making a job that requires its own id 
    let addJob (list: JobList) (makeJob:int->JobItem) =
      lock list (fun ()->  
        let id = list.Count
        list.[id] <- (makeJob id)
        id)
    ///Returns a refernce to the job whos id is given
    let getJob (list: JobList) id = list.[id]
    ///Returns a refernce to the job whos id is given
    let setJob (list: JobList) id = list.[id]
    ///Returns a refernce to the job whos id is given
    let removeJob (list: JobList) id= list.Remove id
    let giveToken  id token (list: JobList)= list.[id].TakenTokens<-token::list.[id].TakenTokens
    type JobListAcessFuncs =
        {
            GetJob:JobID->JobItem
            RemoveJob:JobID->bool
            AddJob:(int->JobItem) ->int
        }

    let JobListAcessFuncs jobList=
        {
            GetJob=getJob jobList
            RemoveJob=removeJob jobList
            AddJob= addJob jobList
        }
