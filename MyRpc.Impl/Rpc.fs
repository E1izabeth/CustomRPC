namespace MyRpc.Impl

open System
open System.Collections.Generic
open System.Linq
open System.Net
open System.Runtime.CompilerServices
open System.Text
open System.Threading.Tasks
open MyRpc.Model
open MyRpc.Impl.Transport
open MyRpc.Impl.Channel

type internal GenericProtocol<'TEndPoint, 'TPacket, 'TMessage>(transport : IRpcTransport<'TEndPoint, 'TPacket>, serializerFabric : IRpcSerializerFabric<'TMessage, 'TPacket>) =
    let _transport = transport
    let _serializerFabric = serializerFabric
            
    interface IRpcProtocol<'TEndPoint, 'TPacket, 'TMessage> with
        member this.Transport = _transport
        member this.SerializerFabric = _serializerFabric

[<AbstractClass; Sealed; Extension>]
type public Rpc private () =

    [<DefaultValue>] static val mutable private _binarySerializer: BinarySerializerImpl.MyBinarySerializerFabric
    [<DefaultValue>] static val mutable private _jsonSerializer: JsonSerializerImpl.JsonSerializerFabric

    static member private init() =
        if (Rpc._binarySerializer :> obj = null) then
            Rpc._binarySerializer <- new BinarySerializerImpl.MyBinarySerializerFabric()
        if (Rpc._jsonSerializer :> obj = null) then
            Rpc._jsonSerializer <- new JsonSerializerImpl.JsonSerializerFabric()

    static member public BinarySerializer
        with get() : IRpcSerializerFabric<Object, byte[]> = Rpc.init(); upcast Rpc._binarySerializer
    
    static member public JsonSerializer
        with get() : IRpcSerializerFabric<Object, byte[]> = Rpc.init(); upcast Rpc._jsonSerializer
    
    static member public TcpTransport with get() : IRpcTransport<IPEndPoint, byte[]> = ChunkedTcpTransport.Instance
    
    static member public GenericHost
        with get() : IRpcHost<Object> = GenericRpcHost.Instance

    [<Extension>]
    static member public MakeProtocol<'TEndPoint, 'TPacket, 'TMessage>(transport : IRpcTransport<'TEndPoint, 'TPacket>, serializer : IRpcSerializerFabric<'TMessage, 'TPacket>): IRpcProtocol<'TEndPoint, 'TPacket, 'TMessage> =
        upcast GenericProtocol<'TEndPoint, 'TPacket, 'TMessage>(transport, serializer)

    [<Extension>]
    static member public Helper<'TMessage>(host : IRpcHost<'TMessage>) : IRpcHostHelper<'TMessage> =
        upcast HostHelper<'TMessage>(host)
   