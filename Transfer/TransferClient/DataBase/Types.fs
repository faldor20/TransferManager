namespace TransferClient.DataBase

open System.Collections.Generic
open System.Threading
open SharedFs.SharedTypes

module Types =
    type Set = string -> int ->TransferData -> unit
    type Add = string  -> TransferData -> int
    type Get = string -> int -> TransferData
    type DataBaseAcessFuncs={
        Set:Set
        Get:Get
        Add:Add
    }
    type SetSpecific = TransferData  -> unit
    type GetSpecific = unit-> TransferData
    ///For gettting a specific transdata from a prefilled dbAdress
    type TransDataAcessFuncs={
        Set:SetSpecific
        Get:GetSpecific
    }
    ///Makes a new TransDataAcessFuncs object that allways points to a specific groupName and index in a DB
    ///just partially applies "Set" and "GET" with groupName and index
    let TransDataAcessFuncs (DBFuncs:DataBaseAcessFuncs) groupName index=
        {
            Set= DBFuncs.Set groupName index 
            Get= (fun x -> DBFuncs.Get groupName index)
        }
