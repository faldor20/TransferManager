# FileFlowFaster
It moves files, and it does it well.
Features:
- WebUI for monitoring transfers(cancelation,reordering etc)
- Transcoding using ffmpeg for video files
- sender receiver trnascoe functionality that allows for transfering video as a compressed format and storing it at either end in a more editable format
  eg: IMX30( 30mb/s intra only sd footage) -> Streamed as h264   -> IMX30
      |Sender                              | data streamed via tcp | recieved and re-encoded
- sending files via ftp
- limiting concurrent transfers in a kind of heirachy
  eg:        total(6)
           /       \
   outbound(4)     inbound(4)
      /            /           \
   NAS(3)    Backups(1)      VidServer(4)   
  each groups number represents the max number of concurrent transfers of that group. each sub group allso belows to the groups above it.
  in this example: 
    - The total can never exceede 6. Maybe you just don't want to have to keep an eye on more trnasfers than this
    - outbound and inbound are sneding and receiving over the internet. say you have 100^/100v internet. So you decide you can do 4 concurrently each way
      - NAS has a lot of smaller files you are sending out. you want a few concurrent streams to minimize speed loss from swtching the many files
      - Backups
        Automatically copy some kinf of backup to a local storage device. Its one huge file so there is no reason to run more than one transfer at once.
      - VidServer you want to opy video from a ery high quality remote storage to you but ina  lower resolution. 
        A single encode doesn't max out your cpu so you want to be able to run a few at once
- Maybe more stuff at some point.
## Features that do not exist:
- Good documentation. its a big complex machine, many parts still change.

## How it all works
  The basic gist is as follows:
  --Do some clientmanager connection and initialisation stuff --
  ###Main loop:
  1.(Watcher) Files get found by the watcher
  2.(TransferClient) Each file gets given to the scheduler where a job is made and added to the jobdb
  3.(JobDB) The jobdb responds to various events and shuffels jobs about. Once that job is ready to run 
    (the file has stopped growing nad it has all its needed tokens
      (Each job needs a token fomr every group it is a part of. Each group starts with a number of tokens equal to the max jobs it allows.
        Once a job takes a token it is unavaialable till it is returned when the job is complete.))
    (The events are:
      1. new job
      2. job finishes (deleted, cancelled success, failure, whatever)
      3. reordering)
      The jobDB does nothing except when one of those events occurs. IT has no internal loop or anythig like that.
  4.(JoBD) The Job Is run, which starts the mover code. (Mover) This may mean starting ffmpeg, tcp,ftp or any other process that is part of the jobs movement
  5.(TransferClient) job finsishes cancels etc
  6.(JobDB) tokens are released
  
  
