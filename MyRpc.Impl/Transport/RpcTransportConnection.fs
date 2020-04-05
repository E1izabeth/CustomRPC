namespace MyRpc.Impl.Transport

open MyRpc.Model
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading.Tasks

#nowarn "58"

type RpcTransportConnection(sck: Socket) =
    let _onError = new DelegateEvent<Action<Exception>>()
    let _onClosed = new DelegateEvent<Action>()

    let _stream = new NetworkStream(sck)

    do
        sck.NoDelay <- true

    interface IRpcTransportConnection<IPEndPoint, byte[]> with
        [<CLIEvent>] member this.OnError = _onError.Publish
        member this.RemoteEndPoint = sck.RemoteEndPoint :?> IPEndPoint

        [<CLIEvent>] member this.OnClosed = _onClosed.Publish

        member this.Dispose() = 
            sck.Close()
            _stream.Dispose()

        member this.ReceivePacketAsync onReceived =
            let lenBuff = Array.zeroCreate<byte>(4)
            _stream.BeginRead(lenBuff, 0, 4, fun ac1 -> (
                if (_stream.EndRead(ac1) < 4) then
                    raise(new NotImplementedException())

                let len = BitConverter.ToInt32(lenBuff, 0)
                let pcktBuff = Array.zeroCreate<byte>(len)
                _stream.BeginRead(pcktBuff , 0, len, fun ac2 -> (
                    if (_stream.EndRead(ac2) < len) then
                        raise(new NotImplementedException())

                    onReceived.Invoke(pcktBuff)
                ), null) |> ignore
            ), null) |> ignore

        member this.SendPacketAsync packet onSent =
            _stream.BeginWrite(BitConverter.GetBytes(packet.Length), 0, 4, fun ar1 -> (
                _stream.EndWrite(ar1)
                _stream.BeginWrite(packet, 0, packet.Length, fun ar2 -> (
                    _stream.EndWrite(ar2)
                    onSent.Invoke()
                ), null) |> ignore
            ), null) |> ignore

