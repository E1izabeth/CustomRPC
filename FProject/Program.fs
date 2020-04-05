
open MyRpc.Impl.Transport
open System.Threading
open System.Runtime.InteropServices.WindowsRuntime
open System
open System.Net
open System.Net.Sockets
open MyRpc.Impl
open MyRpc.Model

type public IApple =
    inherit IRemoteObject
    abstract Chrum: unit -> unit
    abstract GetInfo: unit -> string

type Apple() =
    let mutable rest = 100.0
    
    interface IApple with

        member this.GetInfo() =
            if rest <= 0.0 then 
                "No apple"
            else 
                rest.ToString()+"% осталось."

        member this.Chrum() =
            lock this (fun () ->
                if rest >= 25.0 - 0.00000001 then 
                    rest <- rest - 25.0
            )

type public IFridge =
    inherit IRemoteObject
    abstract GetApple : unit -> IApple
        
type Fridge() =
    let myApple = new Apple()

    interface IFridge with
        member this.GetApple() = 
            upcast myApple

let _host = Rpc.GenericHost.Helper().ForService<IFridge>()
let _protocol = Rpc.TcpTransport.MakeProtocol(Rpc.JsonSerializer)

let DoWithApple() =
    let fridge = new Fridge()
    let mutable clients = (Map.empty, 0)

    use listener = _host.Listen _protocol (IPEndPoint(IPAddress.Any, 12345))
    listener.Start()

    let rec acceptFunc = new Action<IRpcChannelAcceptContext<IPEndPoint, IFridge>>(fun c -> 
        let chan = c.Confirm(fridge)
        lock listener (fun () -> 
            let (map, chanId) = clients
            chan.add_OnClosed(fun () -> lock listener (fun () -> 
                clients <- let (m, c) = clients in (m.Remove chanId, c)
            ))
            clients <- (map.Add(chanId, chan), chanId + 1)
        )
        chan.Start()
        listener.AcceptChannelAsync acceptFunc
    )

    listener.AcceptChannelAsync acceptFunc
    Console.WriteLine("Working. Press Q to exit")
    while Console.ReadKey(true).Key <> ConsoleKey.Q do ()

let DoWithoutApple() =
    use cnn = _host.Connect _protocol (IPEndPoint(IPAddress.Loopback, 12345))
    cnn.Start()
    let remoteApple = cnn.Service.GetApple()
    
    remoteApple.GetInfo() |> Console.WriteLine
    Console.WriteLine("Press any key to chrum or Q to exit")
    while Console.ReadKey().Key <> ConsoleKey.Q do
        remoteApple.Chrum()
        remoteApple.GetInfo() |> Console.WriteLine

[<EntryPoint>]
let main argv = 
    Console.WriteLine("Press S for server mode or other key for client mode.. Server has Apple. Client doesn't have")
    if Console.ReadKey().Key = ConsoleKey.S then
        DoWithApple()
    else
        DoWithoutApple()
    0 // return an integer exit code
