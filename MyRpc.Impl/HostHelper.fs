namespace MyRpc.Impl

open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks

type public HostHelper<'TMessage> (host : IRpcHost<'TMessage>) =
    let _host = host

    interface IRpcHostHelper<'TMessage> with
        member this.ForService<'TService>() : IRpcServiceHost<'TMessage, 'TService> =
            upcast new GenericServiceHost<'TMessage, 'TService>(_host)
        
        member this.Host = _host

        
[<Serializable; Sealed>]
type internal ObjInfo(id, ifaces) = 
    member this.Id: int64 = id
    member this.IfaceNames: string[] = ifaces

