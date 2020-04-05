namespace MyRpc.Impl.BinarySerializerImpl

open MyRpc.Impl
open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks
open System.Reflection
open System.Reflection.Emit
open System.Runtime.Serialization.Formatters.Binary
open System.Runtime.Serialization
open System.IO

type MyBinaryFormatterSerializationSurrogate(marshaller: IRpcSerializationMarshaller) =
    interface ISerializationSurrogate with
        member this.GetObjectData(o: obj, info: SerializationInfo, context: StreamingContext) =
            let o2 = marshaller.Marshal(o)

            let t = o2.GetType()
            let members = FormatterServices.GetSerializableMembers(t, context)
            let values = FormatterServices.GetObjectData(o2, members)
            
            for i in 0 .. members.Length - 1 do
                info.AddValue(members.[i].Name, values.[i])

            info.SetType(t)
            
        member this.SetObjectData(o: obj, info: SerializationInfo, context: StreamingContext, selector: ISurrogateSelector): obj =
            
            let members = FormatterServices.GetSerializableMembers(o.GetType(), context)
            let values = Array.zeroCreate(members.Length)
            for i in 0 .. members.Length - 1 do
                values.[i] <- info.GetValue(members.[i].Name, match members.[i] with
                                                              | :? FieldInfo as f    -> f.FieldType
                                                              | :? PropertyInfo as p -> p.PropertyType
                                                              |_                     -> raise(NotImplementedException()))

            marshaller.Unmarshal(FormatterServices.PopulateObjectMembers(o, members, values))

type MyBinaryFormatterSurrogateSelector(marshaller: IRpcSerializationMarshaller)  =
    inherit SurrogateSelector()
    override this.GetSurrogate(t: Type, context: StreamingContext, selector: byref<ISurrogateSelector>): ISerializationSurrogate =
        if t = null then
            raise(ArgumentNullException "type")
        else if typeof<IRemoteObject>.IsAssignableFrom t || typeof<ObjInfo>.IsAssignableFrom t then
            selector <- this
            upcast new MyBinaryFormatterSerializationSurrogate(marshaller)
        else 
            base.GetSurrogate(t, context, &selector)

type public MyBinarySerializer(marshaller: IRpcSerializationMarshaller) =
    let _serializer = new BinaryFormatter()
    do
        _serializer.SurrogateSelector <- new MyBinaryFormatterSurrogateSelector(marshaller)

    interface IRpcSerializer<Object, byte[]> with
        member this.Deserialize(packet) = _serializer.Deserialize(new MemoryStream(packet))
        member this.Serialize(obj) = 
            let ms = new MemoryStream()
            _serializer.Serialize(ms, obj)
            ms.ToArray()

type public MyBinarySerializerContext(marshaller: IRpcSerializationMarshaller) =
    interface IRpcSerializerContext<Object, byte[]> with
        member IRpcSerializerContext.CreateSerializer() = upcast new MyBinarySerializer(marshaller)
        
type public MyBinarySerializerFabric() =
    interface IRpcSerializerFabric<Object, byte[]> with
        member this.CreateContext(marshaller) = upcast new MyBinarySerializerContext(marshaller)
   