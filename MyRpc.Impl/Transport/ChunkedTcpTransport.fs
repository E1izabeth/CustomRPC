namespace MyRpc.Impl.Transport

open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open System.Globalization


type ChunkedTcpTransport private () =

    //static member val public Instance = new ChunkedTcpTransport() :> IRpcTransport<IPEndPoint, byte[]> with get

    static let _instance = lazy(new ChunkedTcpTransport());
    static member public Instance : IRpcTransport<IPEndPoint, byte[]> = upcast _instance.Value

    interface IRpcTransport<IPEndPoint, byte[]> with

        member this.CreateConnection(remoteEndPoint : IPEndPoint) =
            let sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            sck.Connect(remoteEndPoint)
            upcast new RpcTransportConnection(sck)

        member this.CreateListener(localEndPoint: IPEndPoint) =
            let listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            listenSocket.Bind(localEndPoint)
            upcast new RpcTransportListener(listenSocket)
