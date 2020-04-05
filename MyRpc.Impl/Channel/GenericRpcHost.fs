namespace MyRpc.Impl.Channel

open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks

type internal GenericRpcHost private () =
    static member val public Instance = new GenericRpcHost() :> IRpcHost<Object> with get

    interface IRpcHost<Object> with

        member this.Listen<'TEndPoint, 'TPacket, 'TService> protocol localEndPoint = 
            upcast new GenericRpcChannelListener<'TEndPoint, 'TService, 'TPacket>(protocol.SerializerFabric, protocol.Transport.CreateListener(localEndPoint))

        member this.Connect<'TEndPoint, 'TPacket, 'TService> protocol remoteEndPoint =
            upcast new GenericRpcChannel<'TService, 'TEndPoint, 'TPacket>(protocol.SerializerFabric, protocol.Transport.CreateConnection(remoteEndPoint))
