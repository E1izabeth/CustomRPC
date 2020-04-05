namespace MyRpc.Impl.JsonSerializerImpl

open FSharpLecture
open ParserLibrary
open MyRpc.Model
open System
open System.Collections.Generic
open System.Linq
open System.Text
open System.Threading.Tasks
open System.IO
open System.Reflection
open FSharpLecture.FSharpLecture

   
type public JsonSerializer(marshaller: IRpcSerializationMarshaller) =
    //crutch
    //if the JsonSerializer falls off, uncomment it
    //static do
        //typeof<JValue>.Assembly.EntryPoint.Invoke(null, null) |> ignore

    interface IRpcSerializer<Object, byte[]> with

        member this.Serialize(obj) = 
            //(JsonSerializerWriter marshaller).WriteInstance(obj, true, true) |> FSharpLecture.stringify |> Encoding.UTF8.GetBytes
            let json = (JsonSerializerWriter marshaller).WriteInstance(obj, true, true)
            let str = FSharpLecture.stringify json
            Encoding.UTF8.GetBytes str

        member this.Deserialize(packet) =
            match FSharpLecture.parse(Encoding.UTF8.GetString(packet)) with
            | ParserLibrary.Success (value, input) -> (JsonSerializerReader marshaller).ReadInstance(null, true, value)
            | ParserLibrary.Failure (label,error,parserPos) -> raise(NotImplementedException())

type public JsonSerializerContext(marshaller: IRpcSerializationMarshaller) =
    interface IRpcSerializerContext<Object, byte[]> with
        member IRpcSerializerContext.CreateSerializer() = upcast new JsonSerializer(marshaller)
        
type public JsonSerializerFabric() =
    interface IRpcSerializerFabric<Object, byte[]> with
        member this.CreateContext(marshaller) = upcast new JsonSerializerContext(marshaller)
   