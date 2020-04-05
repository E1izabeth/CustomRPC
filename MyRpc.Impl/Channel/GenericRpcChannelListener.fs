namespace MyRpc.Impl.Channel

open MyRpc.Model;
open System;
open System.Collections.Generic;
open System.Linq;
open System.Net;
open System.Text;
open System.Threading.Tasks;

#nowarn "58"

type public GenericRpcChannelAcceptContext<'TService, 'TEndPoint, 'TPacket>(serializerFabric: IRpcSerializerFabric<obj, 'TPacket>, connectionAcceptCtx: IRpcTransportAcceptContext<'TEndPoint, 'TPacket>) =
    interface IRpcChannelAcceptContext<'TEndPoint, 'TService> with
    
        member this.RemoteEndPoint = connectionAcceptCtx.RemoteEndPoint

        member this.Confirm service = 
            upcast new GenericRpcChannel<_, _, _>(serializerFabric, connectionAcceptCtx.Confirm(), service)

        member this.Reject() = 
            connectionAcceptCtx.Reject()

type internal GenericRpcChannelListener<'TEndPoint, 'TService, 'TPacket>(serializerFabric: IRpcSerializerFabric<obj, 'TPacket>, transportListener: IRpcTransportListener<'TEndPoint, 'TPacket>) =

    do
        transportListener.add_OnError(fun ex -> ())

    interface IRpcChannelListener<'TEndPoint, 'TService> with
        member this.LocalEndPoint = transportListener.LocalEndPoint

        member this.AcceptChannelAsync onAccept = 
            transportListener.AcceptAsync(fun ctx -> (
                onAccept.Invoke(new GenericRpcChannelAcceptContext<_, _, _>(serializerFabric, ctx))
            ))

        member this.Dispose() = 
            transportListener.Dispose()

        member this.Start() =
            transportListener.Start()
