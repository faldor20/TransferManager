namespace SharedFs
open System
module SharedTypes =
    type TransferStatus=
        |   Waiting=1
        |  Copying=2
        |  Complete=3
        |  Cancelled=4
        |  Failed=5
    [<CLIMutable>]
    type TransferData={
         Percentage:float 
         FileSize :float
         FileRemaining: float
         Speed:float
         Destination:string
         Source:string
         Status:TransferStatus
         StartTime:DateTime
         EndTime:DateTime
         id:int 
       }
    
     

