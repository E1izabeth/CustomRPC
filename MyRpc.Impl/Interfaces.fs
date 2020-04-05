namespace MyRpc.Impl

open MyRpc.Model;
open System;
open System.Collections.Generic;
open System.Linq;
open System.Text;
open System.Threading.Tasks;

type public IRpcServiceHost<'TMessage, 'TService> =
    abstract Host : IRpcHost<'TMessage> with get
        
    abstract Listen<'TEndPoint, 'TPacket> : IRpcProtocol<'TEndPoint, 'TPacket, 'TMessage> -> 'TEndPoint -> IRpcChannelListener<'TEndPoint, 'TService> 
    abstract Connect<'TEndPoint, 'TPacket> : IRpcProtocol<'TEndPoint, 'TPacket, 'TMessage> -> 'TEndPoint -> IRpcChannel<'TService>


type public IRpcHostHelper<'TMessage> =
    abstract Host : IRpcHost<'TMessage> with get

    abstract ForService<'TService> : unit -> IRpcServiceHost<'TMessage, 'TService>
