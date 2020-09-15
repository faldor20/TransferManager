namespace TransferClient.DataBase

open System.Collections.Generic
open System.Threading
open SharedFs.SharedTypes

module Types =


    
    
    type Set = string list -> int ->TransferData -> unit
    /// groupName->data->index
    type Add = string list -> TransferData -> unit
    type Get = string list -> int -> TransferData
  
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
    let TransDataAcessFuncs (dbFuncs:DataBaseAcessFuncs) keys index=
        {
            Set= dbFuncs.Set keys index 
            Get= (fun x -> dbFuncs.Get keys index)
        }
