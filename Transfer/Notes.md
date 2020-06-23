# Progress:

TODO: 

1. get ftp running.

    1.figure out wiat to do with the destinationinfo check that happens to see if it is a valid dir. maybe do an ftp connection with a try catch arround



write some proper test for the DATA portion
write the frontend
make  generate exmaple json function if the watchdirs.json cannot be found
Allow users to cancel a transfer
i need to find a way to get rid of transfers that never properly start, i can look for a file not found exception from my task starter
The move function isn't corerctly firing the progress events, it need sto be set back to a copy function

    probably need to setup a cancelation token type thing


	i get a stack overflow, maybe i need to limit the number of concurrent jobs

for osme reason therea rea just a few files that a simply cannot delte after theya re coppied, it says i don't have write aces

for longer copies program keeps running fater it is force closed

# Design 

i think i need to do copying and then deletion assuming all tasks related to it ave completed. That way we can have multilple tasks copy from each loccation.
    i could do this in two ways, i could place a list of locks associated with each file and then delete them once all locks have been unlocked
    or a couldust try to do the delte after a while and accept that it will fail if i try 

## make a fork of ioextension library and add a cencelation token to the moveasync function
    we can use this to allow cancelling a transfer