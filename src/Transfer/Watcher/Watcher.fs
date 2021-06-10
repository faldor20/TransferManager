namespace FileWatcher

open System.IO
open Mover.Types
open FluentFTP
open FSharp.Control
open LoggingFsharp
module Watcher =

    
    let private checkForNewFilesFTP (connection:FtpClient) (ignoreList: string []) (folder)=
        try
            let fileList=connection.GetListing(folder)
            let fileNames=
                fileList
                |>Array.filter(fun item->item.Type=FtpFileSystemObjectType.File)
                |>Array.map(fun x-> { Path= x.FullName; FTPFileInfo=Some x})
                |>Array.filter (fun x-> not(ignoreList|>Array.contains x.Path) ) 
                //TODO: this may have to be changed becuase ignorelist now takes ino account fileinfo like acessedtime
            fileNames
        with
            |e->Lgerrorf ("Exception occured while checking for new ftp files in folder %s \n Excpetion: %A") folder e
                Array.empty
    //TODO i could make this function use some kind of metadata of the file to create a unique id instead of just the file name
    //creation date+name should work
    ///Returns the filePaths of files not part of the ignorelist
    let private checkForNewFiles2 (ignoreList: string[]) (folder) =
        DirectoryInfo( folder).GetFiles()
        |>Array.map(fun fileInfo->fileInfo.FullName)
        |>Array.except ignoreList
        |>Array.map(fun fileInfo -> { Path=fileInfo;FTPFileInfo=None}) 
      
    ///**checkInterval** is in milliseconds
    ///Returns an async seqence of files and an event to reset the list of ignored files.
    /// The sequence will contain all files added to the sourceDir, now and forever.
    /// If you process the asyncseq with map that map will never finish, becuase the squence never really ends.
    ///
    /// files are found by name. This means once a file has been found files with the same name will be ignored untill the resetEvent is triggered
    /// Internally its just an inifnite loop that looks at all files every checkInterval and pushes ones with a new name to the seq
    let getNewFiles (ftpData:FTPData option) sourceDir (checkInterval:int)    =
       
        let resetEvent = new Event<unit>()
        (asyncSeq{ 

            let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
            let resetIgnores _ =ignoreList<-Array.empty
            resetEvent.Publish.Add(resetIgnores)

            use nullableFTPConnection= 
                match ftpData with 
                |Some x->
                    Lginfof "{Watcher} Connecting to ftp:%A"x
                    let con= new FtpClient(x.Host,x.User,x.Password)
                    con.Connect()
                    con
                |None -> null
            Lginfof "{Watcher} Watching : %A"sourceDir
            while true do
                try
                    let newFilesFunc=
                        match nullableFTPConnection with 
                        |null-> checkForNewFiles2 
                        |con->checkForNewFilesFTP con 
                    let newFiles=newFilesFunc ignoreList sourceDir  
                    if newFiles.Length>0 then
                        Lgdebugf"{Watcher} Found new files yielding now "
                        yield newFiles
                        ignoreList<- ignoreList|> Array.append (newFiles|> Array.map(fun x->x.Path))
                with|e->Lgerrorf "Exception thrown while doing watched folder check for folder %s exception:%A" sourceDir e
                do! Async.Sleep(checkInterval);
        },resetEvent)

   