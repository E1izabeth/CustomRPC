namespace MyRpc.Impl.Channel

open MyRpc.Impl
open MyRpc.Model
open MyRpc.Impl.BinarySerializerImpl
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks
open System.IO
open System.Threading
open System.Runtime.Remoting.Proxies
open System.Runtime.Remoting.Messaging
open System.Runtime.Remoting
open System.Reflection.Emit
open System.Reflection

[<Serializable; Sealed>]
type internal MethodSpec(name, declaringType) =
    member this.Name : string = name
    member this.DeclaringType : string = declaringType
    override this.ToString() = "MethodSpec[" + declaringType + "::" + name + "]"
    
[<Serializable>]
type internal CallResult =
    | Success of result: obj// * outs : obj[]
    | Error of info: Exception

[<Serializable>]
type internal Message =
    | Call of callId: int * objId: int64 * methodSpec: MethodSpec * args :obj[] 
    | Result of callId: int * data: CallResult
    | Release of objId: int64

type internal CallContext(req) =
    member public this.Request : Message = req
    member val Result = option<CallResult>.None with get, set

type internal IMethodCallHandler =
    abstract member DoCall: int64 -> MethodSpec -> obj[] -> obj
    
type internal Proxy(t: Type, objId : int64, handler: IMethodCallHandler) =
    inherit RealProxy(t)
    
    override this.Invoke msg =
        let callMsg = msg :?> IMethodCallMessage

        let result = if callMsg.MethodBase.DeclaringType = typeof<Object> && callMsg.MethodName = "GetType" then t :> obj
                     else handler.DoCall objId (MethodSpec(callMsg.MethodName, callMsg.MethodBase.DeclaringType.AssemblyQualifiedName)) callMsg.Args

        upcast ReturnMessage(result, callMsg.Args, callMsg.Args.Length, callMsg.LogicalCallContext, callMsg)


type internal GenericRpcChannel<'TService,'TEndPoint, 'TPacket>(serializerFabric: IRpcSerializerFabric<Object, 'TPacket>, connection: IRpcTransportConnection<'TEndPoint, 'TPacket>, ?service: 'TService) as this =

    let _localObjs = new Dictionary<int64, obj>();
    let _remoteObjs = new Dictionary<int64, WeakReference>();
    let mutable _localObjsCount = 0L;

    let _onClosed = new DelegateEvent<Action>() 
    let _serializationContext = serializerFabric.CreateContext(this)

    let mutable _isSending = false
    let _packetsQueue = new Queue<'TPacket>()
    
    let _callCtxById = new Dictionary<int, CallContext>()
    let mutable _callIdCount = 0

    let _service = if service.IsSome then 
                       _localObjs.Add(0L, service.Value)
                       service.Value
                   else 
                       let proxy = (Proxy(typeof<'TService>, 0L, this).GetTransparentProxy() :?> 'TService)
                       _remoteObjs.Add(0L, WeakReference(proxy))
                       proxy

    member private this.CleanupRemoteObjs() =
        lock _remoteObjs (fun () -> for o in _remoteObjs.ToArray() do 
                                        if not o.Value.IsAlive then
                                            _remoteObjs.Remove(o.Key) |> ignore)

    member private this.OnMessage msg =
        this.ReceivePacketAndHandleAsync()
        match msg with
        | Call(callId, objId, methodSpec, args) -> try 
                                                       match lock _localObjs (fun () -> _localObjs.TryGetValue objId) with
                                                       | true, o -> let method = Type.GetType(methodSpec.DeclaringType).GetMethod(methodSpec.Name)
                                                                    let returnValue = method.Invoke(o, args)
                                                                    this.SendMessage(Result(callId, Success(returnValue)))
                                                       |_        -> raise(RemotingException("Remote object not found"))
                                                   with
                                                   | :? Exception as ex -> this.SendMessage(Result(callId, Error(ex)))

        | Result(callId, info)                  -> let ctx = (lock _callCtxById (fun () -> _callCtxById.[callId]))
                                                   lock ctx.Request (fun () -> ctx.Result <- Some(info); Monitor.Pulse(ctx.Request))

        | Release(objId)                        -> lock _localObjs (fun () -> _localObjs.Remove(objId) |> ignore)

        |_ -> raise(new NotImplementedException())
       
    member private this.SendMessageProc ar =
        lock _packetsQueue (fun () ->
            if _packetsQueue.Count > 0 then
                connection.SendPacketAsync (_packetsQueue.Dequeue()) (Action this.SendMessageProc)
            else
                _isSending <- false
        )

    member private this.SendMessage msg =
        let pckt = _serializationContext.CreateSerializer().Serialize(msg)
        lock _packetsQueue (fun ()->
            if _isSending then
                _packetsQueue.Enqueue pckt
            else 
                _isSending <- true
                connection.SendPacketAsync pckt (Action this.SendMessageProc)
        )        

    member private this.ReceivePacketAndHandleAsync() = 
        connection.ReceivePacketAsync(Action<_> (fun pckt -> match _serializationContext.CreateSerializer().Deserialize(pckt) with
                                                             | :? Message as msg -> this.OnMessage msg
                                                             |_ -> raise(new NotImplementedException()) ))
    
    interface IMethodCallHandler with
        member this.DoCall objId methodSpec args =
            let callId = Interlocked.Increment(&_callIdCount)
            let callMsg = Call(callId, objId, methodSpec, args)
            let callCtx = CallContext callMsg

            printfn "%s" (callMsg.ToString())
        
            lock _callCtxById (fun () -> _callCtxById.Add(callId, callCtx))

            this.SendMessage callMsg
            lock callMsg (fun () -> Monitor.Wait callMsg |> ignore) // TODO: timeout

            lock _callCtxById (fun () -> _callCtxById.Remove callId |> ignore)
            match callCtx.Result.Value with
            | Success(retVal) -> retVal
            | Error(ex)       -> raise(RemotingException("Exception occured on the remote side of the RPC channel", ex))
            

    member private this.MarshalImpl(o: obj): ObjInfo =
        let objId = Interlocked.Increment(&_localObjsCount)
        lock _localObjs (fun () -> _localObjs.Add(objId, o))
        new ObjInfo(objId, o.GetType().GetInterfaces().Select(fun t -> t.AssemblyQualifiedName).ToArray())


    member private this.UnmashalImpl(info: ObjInfo): obj =
        match lock _remoteObjs (fun () -> _remoteObjs.TryGetValue info.Id) with
        | true, weakRef -> try weakRef.Target
                           with | :? InvalidOperationException -> this.InstantiateProxyObj(info)
        |_              -> this.InstantiateProxyObj(info)

    member private this.InstantiateProxyObj(info: ObjInfo): obj =
        let ifaces = info.IfaceNames.Select(fun s -> Type.GetType(s)).Where(fun t -> t <> null).ToArray()
        let asmName = Guid.NewGuid().ToString()
        let asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(asmName), AssemblyBuilderAccess.Run)
        let moduleBuilder = asmBuilder.DefineDynamicModule(asmName)
        let typeBuilder = moduleBuilder.DefineType(asmName, TypeAttributes.Interface ||| TypeAttributes.Abstract ||| TypeAttributes.Public, null, ifaces)
        let ifaceType = typeBuilder.CreateType()
        // moduleBuilder.CreateGlobalFunctions()
        let obj = Proxy(ifaceType, info.Id, this).GetTransparentProxy()
        lock _remoteObjs (fun () -> _remoteObjs.[info.Id] <- new WeakReference(obj))
        obj

    interface IRpcChannel<'TService> with 

        member val Service = _service

        [<CLIEvent>] member this.OnClosed = _onClosed.Publish

        member this.Start() = this.ReceivePacketAndHandleAsync()
        member this.Dispose() = connection.Dispose()

    interface IRpcSerializationMarshaller with

        member this.Marshal o =
            match o with
            | :? IRemoteObject -> upcast this.MarshalImpl o
            |_                 -> o

        member this.Unmarshal o =
            match o with
            | :? ObjInfo as info -> this.UnmashalImpl info
            |_                   -> o
