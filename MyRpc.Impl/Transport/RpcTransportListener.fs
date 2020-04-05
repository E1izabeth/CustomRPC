namespace MyRpc.Impl.Transport

open MyRpc.Model;
open System;
open System.Collections.Generic;
open System.Linq;
open System.Net;
open System.Net.Sockets;
open System.Text;
open System.Threading.Tasks;

type RpcTransportAcceptContext(sck: Socket ) =
    interface IRpcTransportAcceptContext<IPEndPoint, byte[]> with
        member this.RemoteEndPoint = sck.RemoteEndPoint :?> IPEndPoint

        member this.Confirm() =
            upcast new RpcTransportConnection(sck)

        member this.Reject() =
            sck.Close()
            sck.Dispose()


type public RpcTransportListener(listenSocket : Socket) =
    let _listenSocket = listenSocket
    let _onError = new DelegateEvent<Action<Exception>>()

    interface IRpcTransportListener<IPEndPoint, byte[]> with

        [<CLIEvent>] member this.OnError = _onError.Publish
        member this.LocalEndPoint = listenSocket.LocalEndPoint :?> IPEndPoint

        member this.AcceptAsync onAccepted =
            _listenSocket.BeginAccept(fun ar -> try _listenSocket.EndAccept ar |> RpcTransportAcceptContext |> onAccepted.Invoke
                                                with | ex -> _onError.Trigger(Array.ofList([ex]))
                                    , null) |> ignore

        member this.Dispose() = _listenSocket.Close()
        member this.Start() = _listenSocket.Listen(10)

