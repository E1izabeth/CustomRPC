namespace MyRpc.Impl.Channel

open MyRpc.Model
open MyRpc.Impl.Transport
open MyRpc.Impl.BinarySerializerImpl
open System
open System.Collections.Generic
open System.Linq
open System.Net
open System.Text
open System.Threading.Tasks

type public GenericRpcProtocol() =
    let _serializerFabric = new MyBinarySerializerFabric()

    interface IRpcProtocol<IPEndPoint, byte[], Object> with
        member this.Transport = ChunkedTcpTransport.Instance
        member this.SerializerFabric = upcast _serializerFabric
    