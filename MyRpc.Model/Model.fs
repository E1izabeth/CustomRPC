namespace MyRpc.Model
    
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Text
open System.Threading.Tasks

type public IRemoteObject = interface end
    
type public IRpcTransportConnection<'TEndPoint, 'TPacket> =
    inherit IDisposable
    
    [<CLIEvent>]abstract member OnError : IDelegateEvent<Action<Exception>>
    [<CLIEvent>]abstract member OnClosed : IDelegateEvent<Action>

    abstract member RemoteEndPoint : 'TEndPoint

    abstract member SendPacketAsync : 'TPacket -> Action -> unit
    abstract member ReceivePacketAsync : Action<'TPacket> -> unit

type public IRpcTransportAcceptContext<'TEndPoint, 'TPacket> =
    
    abstract member RemoteEndPoint : 'TEndPoint

    abstract member Confirm : unit -> IRpcTransportConnection<'TEndPoint, 'TPacket>
    abstract member Reject : unit -> unit
    

type public IRpcTransportListener<'TEndPoint, 'TPacket> =
    inherit IDisposable
    
    [<CLIEvent>]abstract member OnError : IDelegateEvent<Action<Exception>>

    abstract member LocalEndPoint : 'TEndPoint

    abstract member Start : unit -> unit

    abstract member AcceptAsync : Action<IRpcTransportAcceptContext<'TEndPoint, 'TPacket>> -> unit
    
type public IRpcTransport<'TEndPoint, 'TPacket> = 
    abstract member CreateConnection : 'TEndPoint -> IRpcTransportConnection<'TEndPoint, 'TPacket>
    abstract member CreateListener : 'TEndPoint -> IRpcTransportListener<'TEndPoint, 'TPacket>
   

type public IRpcChannel<'TService> =
    inherit IDisposable
   
    [<CLIEvent>] abstract member OnClosed : IDelegateEvent<Action>

    abstract member Service : 'TService

    abstract member Start : unit -> unit
   
type public IRpcChannelAcceptContext<'TEndPoint, 'TService> =
    
    abstract member RemoteEndPoint : 'TEndPoint

    abstract member Confirm : 'TService-> IRpcChannel<'TService> 
    abstract member Reject : unit -> unit
    

type public IRpcChannelListener<'TEndPoint, 'TSerivce> =
    inherit IDisposable
    
    abstract member LocalEndPoint : 'TEndPoint

    abstract member Start : unit -> unit
    abstract member AcceptChannelAsync : Action<IRpcChannelAcceptContext<'TEndPoint, 'TSerivce>> -> unit

type public IRpcSerializationMarshaller =
    abstract member Marshal : Object -> Object
    abstract member Unmarshal : Object -> Object

type public IRpcSerializer<'TMessage, 'TPacket> =
    abstract member Serialize : 'TMessage -> 'TPacket
    abstract member Deserialize : 'TPacket -> 'TMessage
     
type public IRpcSerializerContext<'TMessage, 'TPacket> =
    abstract member CreateSerializer : unit -> IRpcSerializer<'TMessage, 'TPacket>

type public IRpcSerializerFabric<'TMessage, 'TPacket> =
    abstract member CreateContext : IRpcSerializationMarshaller -> IRpcSerializerContext<'TMessage, 'TPacket>

type public IRpcProtocol<'TEndPoint, 'TPacket, 'TMessage> =
    abstract member Transport : IRpcTransport<'TEndPoint, 'TPacket>
    abstract member SerializerFabric : IRpcSerializerFabric<'TMessage, 'TPacket>

type public IRpcHost<'TMessage> =
    abstract member Listen<'TEndPoint1, 'TPacket1, 'TService1> : IRpcProtocol<'TEndPoint1, 'TPacket1, 'TMessage> -> 'TEndPoint1 -> IRpcChannelListener<'TEndPoint1, 'TService1>
    abstract member Connect<'TEndPoint2, 'TPacket2, 'TService2> : IRpcProtocol<'TEndPoint2, 'TPacket2, 'TMessage> -> 'TEndPoint2 -> IRpcChannel<'TService2>
 