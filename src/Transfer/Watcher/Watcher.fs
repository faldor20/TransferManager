namespace TransferClient

open System.IO
open Mover.Types
open FluentFTP
open FSharp.Control
open LoggingFsharp
module Watcher =

//======================
// Here we have the new version of the scheduling code that uses async sequences
//====================
    let private checkForNewFilesFTP (connection:FtpClient) (ignoreList: string []) (folder)=
        try
            let fileList=connection.GetListing(folder)
            let fileNames=
                fileList
                |>Array.filter(fun item->item.Type=FtpFileSystemObjectType.File)
                |>Array.map(fun x-> { Path= x.FullName;FTPFileInfo=Some x})
                |>Array.filter (fun x-> not(ignoreList|>Array.contains x.Path) ) 
                //TODO: this may have to be changed becuase ignorelist now takes ino account fileinfo like acessedtime
            fileNames
        with
            |e->Lgerrorf ("Exception occured while checking for new ftp files in folder %s \n Excpetion: %A") folder e
                Array.empty
        
    ///Returns the filePaths of files not part of the ignorelist
    let private checkForNewFiles2 (ignoreList: string[]) (folder) =
       
        DirectoryInfo( folder).GetFiles()|>Array.map(fun i-> { Path=i.FullName;FTPFileInfo=None}) |>Array.filter (fun x-> not(ignoreList|>Array.contains x.Path) ) 
        
    

    let getNewFiles (ftpData:FTPData option) sourceDir   =
       
        (asyncSeq{ 
            let mutable ignoreList= Array.empty  //We iterate through the list each pair contains watchdir and a list of the new files in that dir 
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
                do! Async.Sleep(500);
        })

   