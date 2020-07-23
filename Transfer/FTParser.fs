namespace Transfer
module FTPParser=
    let splitAtLastOccurance (input:string) (splitter:string)= 
            let index= input.LastIndexOf splitter
            (input.Remove index,input.Substring (index+1))

    let splitAtFirstOccurance (input:string) (splitter:string)= 
        let index= input.IndexOf splitter
        (input.Remove index,input.Substring (index+1))
    type FtpConectionInfo={
        User:string
        Password:string
        Host:string
        Path:string
    }
    let FtpConectionInfo user pass host path= {User=user;Password=pass;Host=host;Path=path}
    
    let parseFTPstring inp =
        let conInfo, path= splitAtFirstOccurance inp "/"
        let cred ,ip= splitAtLastOccurance conInfo "@"
        let user, pass= splitAtFirstOccurance cred ":"
        printfn "building uri: host: |%s| usr %s password %s path %s" ip user pass path
        {User=user;Path=path;Host=ip;Password=pass}
    