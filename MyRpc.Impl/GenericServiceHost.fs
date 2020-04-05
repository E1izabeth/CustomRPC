namespace MyRpc.Impl

open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks

type public GenericServiceHost<'TMessage, 'TService>(host: IRpcHost<'TMessage>) =
    let _host = host

    interface IRpcServiceHost<'TMessage, 'TService> with
        member this.Host = _host
        
        member this.Listen protocol localEndPoint  =
            _host.Listen<'TEndPoint, 'TPacket, 'TService> protocol localEndPoint
    
        member this.Connect<'TEndPoint, 'TPacket> protocol remoteEndPoint =
            _host.Connect<'TEndPoint, 'TPacket, 'TService> protocol remoteEndPoint
