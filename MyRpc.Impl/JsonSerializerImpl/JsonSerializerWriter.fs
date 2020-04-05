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

type J = 
    static member B(v: bool) = JBool(v)
    static member N(n: float) = JNumber(n)
    static member N(n: int) = JNumber(float(n))
    static member S(s: string) = JString(s)
    static member O(l) = JObject(Map.ofSeq l)
    static member A(l) = JArray(l)
    static member Null = JNull

type public JsonSerializerBase() =
    member this.IsTypeInfoRequired(locationType: Type) =
        locationType = null || not (
               locationType.IsValueType
            || locationType.IsPrimitive
            || locationType.IsEnum
            || (locationType.IsArray && this.IsTypeInfoRequired(locationType.GetElementType()))
            || (locationType.IsClass && locationType.IsSealed)
        )

    member this.CanBeCached(locationType: Type) =
        locationType = null || not(
               locationType.IsValueType
            || locationType.IsPrimitive
            || locationType.IsEnum
        )

type public JsonSerializerWriter(marshaller: IRpcSerializationMarshaller) =
    inherit JsonSerializerBase()

    let _idByObj = Dictionary<obj, int>()
    let _idByType = Dictionary<Type, int>()
    
    // let _primitiveWriters = 

    //let inline (|>>) a b = b a; a
    //let x = 3 |>> printf "%i" |>> printf "%i"
    //let addWithNumber(kv: Dictionary<'K, 'V>, v: 'V): int = kv.Count |>> kv.Add v
    
    let fwd(a: 'T, b: 'T -> unit): 'T = b a; a
    let addWithNumber(kv: Dictionary<'K, _>, v: 'K): int = fwd (kv.Count, fun n -> kv.Add(v, n))
    
    member this.RegisterWrittenInstance(o: obj) =
        match o with
        | :? Type as t when not(_idByType.ContainsKey t) -> addWithNumber(_idByType, t)
        |_                                               -> addWithNumber(_idByObj, o)
    
    member this.WriteTypeSignature(t: Type) : JValue =
        if t.IsGenericType && not(t.IsGenericTypeDefinition) then
            J.O [ 
                ("genTypeDef", this.WriteInstance(t.GetGenericTypeDefinition(), false))
                ("genTypeArgs", this.WriteInstance(t.GetGenericArguments(), false, false))
                ("@typeId", J.N(this.RegisterWrittenInstance(t)))
            ]
        else
            let mutable ct = t
            J.O [
                ("@typeId", J.N(this.RegisterWrittenInstance(t)))
                ("asm", this.WriteInstance(t.Assembly.FullName, false))
                ("ns", this.WriteInstance(t.Namespace, false))
                ("type", this.WriteInstance([| 
                    yield ct.Name
                    while ct.IsNested do 
                        ct <- ct.DeclaringType
                        yield ct.Name 
                |] |> Array.rev, false))
            ]

    member this.WriteInstance(o: obj): JValue                                      = this.WriteInstance(o, true, true)
    member this.WriteInstance(o: obj, withTypeInfo: bool): JValue                  = this.WriteInstance(o, withTypeInfo, true)
    member this.WriteInstance(o: obj, withTypeInfo: bool, withCache: bool): JValue =
        //Type t = obj?.GetType();
            // Console.WriteLine("writing " + (obj == null ? "<NULL>" : obj.GetType().FullName) + " " + (obj == null || obj.ToString() == obj.GetType().ToString() ? "" : obj.ToString()));
        //Console.WriteLine("writing " + (if o = null then "<NULL>" else o.GetType().FullName) + " " + 
        //                               (if o = null || o.ToString() = o.GetType().ToString() then "" else o.ToString()));

        let hasObjId, objId = if o <> null && withCache then _idByObj.TryGetValue o 
                              else                           false, 0
        let hasTypeId, typeId = match o with 
                                | :? Type as typeObj when o <> null && withCache -> _idByType.TryGetValue typeObj
                                |_                                               -> false, 0

        let t = if o <> null then o.GetType() else null

        match o with
        | :? Type when hasTypeId    -> J.O [ ("@typeRef", J.N typeId) ]
        | :? obj  when hasObjId     -> J.O [ ("@objRef", J.N objId) ]
        | null                      -> if withTypeInfo || withCache then J.Null
                                       else raise(InvalidOperationException()) // can't recognize null without type kind token during deserialization
        | :? bool as value          -> J.B value
        | :? obj when t.IsPrimitive -> J.N(Convert.ChangeType(o, typeof<float>) :?> float)
                                        //var (typeKind, primitiveWriter) = _primitiveWriters[t];

                                        //if (withTypeInfo || withCache)
                                        //    _writer.WriteByte((byte)typeKind);

                                        //primitiveWriter(_writer, obj);
        | :? Type as typeRef        -> this.WriteTypeSignature typeRef
        | :? string as str          -> J.S str
            //            {
            //                if (withTypeInfo || withCache)
            //                    _writer.WriteByte((byte)TypeKind.String);

            //                if (withCache)
            //                    this.RegisterWrittenInstance(obj);

            //                _writer.WriteString(str);
            //            }
            //            break;
        | :? Guid as id             -> J.S(id.ToString())
        | :? Enum as value          -> this.WriteEnumInstanceImpl(o, withTypeInfo, withCache)
        | :? Array as arr           -> this.WriteArrayInstanceImpl(arr, withTypeInfo, withCache)
        |_                          -> this.WriteCustomTypeInstanceImpl(o, withTypeInfo, withCache)

    member this.WriteEnumInstanceImpl(o: obj, withTypeInfo: bool, withCache: bool ) =
        let t = o.GetType()
        

        if withTypeInfo || withCache then
            J.O [
                if withTypeInfo then
                    yield ("@objType", this.WriteInstance(t, false))

                yield ("@value", J.S(o.ToString()))
            ]
        else
            J.S(o.ToString())

    member this.WriteArrayInstanceImpl(arr: Array, withTypeInfo: bool, withCache: bool)=
        let elementType = arr.GetType().GetElementType()

        if withTypeInfo || withCache || arr.Rank > 1 then
            J.O [
                if withCache then
                    yield ("@objId", J.N(this.RegisterWrittenInstance arr))
                if arr.Rank > 1 then
                    yield ("@arrayDimensions", this.WriteInstance([| for n in 0 .. arr.Rank - 1 -> arr.GetLength(n) |], false, false))
                if withTypeInfo then
                    yield ("@arrayItemType", this.WriteInstance(elementType, false))
                
                yield ("@arrayItems", J.A [ for o in arr -> this.WriteInstance(o, this.IsTypeInfoRequired(elementType), this.CanBeCached(elementType)) ])
                // yield ("@arrayItems", this.WriteInstance([| for o in arr -> this.WriteInstance(o, this.IsTypeInfoRequired(elementType), this.CanBeCached(elementType)) |], false, false))
            ]
        else
            J.A [ for o in arr -> this.WriteInstance(o, this.IsTypeInfoRequired(elementType), this.CanBeCached(elementType)) ]

    member this.WriteCustomTypeInstanceImpl(mo: obj, withTypeInfo: bool, withCache: bool) =
        let o = marshaller.Marshal(mo)
        let t = o.GetType()
        
        J.O [ 
            if withTypeInfo || withCache then
                //_writer.WriteByte((byte)TypeKind.Custom);
                if withTypeInfo then
                    yield ("@objType", this.WriteInstance(t, false))
            
            if withCache then
                yield ("@objId", J.N(this.RegisterWrittenInstance(o)))

            for f in t.GetFields(BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                -> (f.Name, this.WriteInstance(f.GetValue(o), this.IsTypeInfoRequired(f.FieldType), this.CanBeCached(f.FieldType)))
        ]
        