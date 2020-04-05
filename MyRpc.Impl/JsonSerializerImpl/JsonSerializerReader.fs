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
open System.Runtime.Serialization

type public JsonSerializerReader(marshaller: IRpcSerializationMarshaller) =
    inherit JsonSerializerBase()
    
    let _objById = List<obj>()
    let _typeById = List<Type>()

    let rec ForEachEntryImpl(dims: int[], n: int, indicies: list<int>, act: int -> int[] -> unit): unit = 
        for i in 0 .. dims.[indicies.Length] - 1 do
            if indicies.Length < dims.Length - 1 then
                ForEachEntryImpl(dims, n + i, indicies @ [i], act)
            else
                act (n + i) (List.toArray(List.rev(indicies @ [i])))

    let ForEachEntry(dims: int[], act: int -> int[] -> unit): unit = ForEachEntryImpl(dims, 0, [], act)


    let (|MapValue|_|) key map = Map.tryFind key map

    let (|MapIntValue|_|) key map = match Map.tryFind key map with
                                    |Some jv -> match jv with
                                                |JNumber n -> Some(int(n))
                                                |_         -> None
                                    |_       -> None

    let (|MapBoolValue|_|) key map = match Map.tryFind key map with
                                     |Some jv -> match jv with
                                                 |JBool b -> Some(b)
                                                 |_       -> None
                                     |_       -> None

    let (|MapStringValue|_|) key map = match Map.tryFind key map with
                                       |Some jv -> match jv with
                                                   |JString s -> Some(s)
                                                   |_         -> None
                                       |_       -> None

    member this.TypeIfInfoRequired(t: Type): Type =
        if this.IsTypeInfoRequired(t) then null else t
    
    member this.RegisterReadInstance(o: obj, ?id: int): unit =
        match o with
        | :? Type as t when id.IsNone || _typeById.Count = id.Value -> _typeById.Add(t)
        |_             when id.IsNone || _objById.Count  = id.Value -> _objById.Add(o)
        |_                                       -> raise(InvalidOperationException())
    
    member this.ReadInstance<'T>(json: JValue): 'T                  = this.ReadInstance<'T>(true, json)
    member this.ReadInstance<'T>(withCache: bool, json: JValue): 'T = match this.ReadInstance(typeof<'T>, withCache, json) with
                                                                      | :? 'T as o -> o
                                                                      |_           -> raise(InvalidOperationException())
        
    member this.ReadInstance(t: Type, withCache: bool, json: JValue): obj =
        //_log.WriteLine("instance {0} {{", type);
        //_log.Push();
        

        match json with
        | JString s -> match t with
                       |null                      -> upcast s
                       |_ when t = typeof<string> -> upcast s
                       |_ when t = typeof<Guid>   -> upcast Guid.Parse(s)
                       |_ when t.IsEnum           -> Enum.Parse(t, s, true)
                       |_                         -> raise(InvalidOperationException())
        | JNumber n -> match t with
                       |null                 -> upcast n
                       |_ when t.IsPrimitive -> Convert.ChangeType(n, t)
                       |_                    -> raise(InvalidOperationException())
        | JBool   b -> upcast b
        | JNull     -> null
        | JArray  a -> if t = null then raise(InvalidOperationException())
                       else let elementType = t.GetElementType()
                            let arr = Array.CreateInstance(elementType, a.Length)
                            this.RegisterReadInstance(arr)
                            let elementTypeIfRequired = this.TypeIfInfoRequired(t.GetElementType())
                            for i in 0 .. a.Length - 1 do 
                                arr.SetValue(this.ReadInstance(elementTypeIfRequired, this.CanBeCached(elementTypeIfRequired), a.[i]), i)
                            upcast arr
        | JObject o -> match o with
                       |MapIntValue "@objRef"  objId      -> _objById.[objId]
                       |MapIntValue "@typeRef" typeId     -> upcast _typeById.[typeId]
                       |MapIntValue "@typeId"  typeId     
                         -> match o with
                            | MapValue "genTypeDef" genDef 
                               & MapValue "genTypeArgs" genArgs -> let r = this.ReadInstance<Type>(genDef)
                                                                               .MakeGenericType(this.ReadInstance<Type[]>(false, genArgs))
                                                                   this.RegisterReadInstance(r, typeId);
                                                                   upcast r
                            | MapValue "asm" asmName
                               & MapValue "ns" nsName
                               & MapValue "type" typeName       -> let assemblyFullName = this.ReadInstance<string>(asmName)
                                                                   let namespaceName = this.ReadInstance<string>(nsName)
                                                                   let typeNameParts = this.ReadInstance<string[]>(typeName)
                                                                   let typeFullName = namespaceName + "." + String.Join("+", typeNameParts)
                                                                   let asm = Assembly.Load(assemblyFullName)
                                                                   let r = asm.GetType(typeFullName)
                                                                   this.RegisterReadInstance(r, typeId)
                                                                   upcast r
                            |_                                  -> raise(InvalidOperationException())

                       |MapValue "@arrayItems" items      -> let elements = match items with 
                                                                            |JArray a -> a
                                                                            |_          -> raise(InvalidOperationException())
                                                             let elementType = match o with
                                                                               |MapValue "@arrayItemType" itemType -> this.ReadInstance<Type>(itemType)
                                                                               |_ when t <> null                   -> t.GetElementType()
                                                                               |_                                  -> raise(InvalidOperationException())
                                                             let dimensions  = match o with     
                                                                               |MapValue "@arrayDimensions" arrDims -> this.ReadInstance<int[]>(arrDims)
                                                                               |_                                   -> [| elements.Length |]
                                                             
                                                             let arr = Array.CreateInstance(elementType, dimensions)
                                                             match o with
                                                             |MapIntValue "@objId" objId -> this.RegisterReadInstance(arr, objId)
                                                             |_                          -> this.RegisterReadInstance(arr)

                                                             let elementTypeIfRequired = this.TypeIfInfoRequired(t.GetElementType())

                                                             ForEachEntry(dimensions, (fun n nn ->  
                                                                 arr.SetValue(this.ReadInstance(elementTypeIfRequired, this.CanBeCached(elementTypeIfRequired), elements.[n]), nn)
                                                             ))

                                                             upcast arr
                                                                                
                       |_ -> let objType = match o with
                                           |MapValue "@objType" ot -> this.ReadInstance<Type>(ot)
                                           |_ when t <> null       -> t
                                           |_                      -> raise(InvalidOperationException())

                             if objType.IsEnum then match o with
                                                    |MapStringValue "@value" s -> Enum.Parse(t, s, true)
                                                    |_                         -> raise(InvalidOperationException())
                             else let r = FormatterServices.GetUninitializedObject(objType)
                                  match o with
                                  |MapIntValue "@objId" objId -> this.RegisterReadInstance(r, objId)
                                  |_                          -> this.RegisterReadInstance(r)
                                  
                                  for f in objType.GetFields(BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic) do
                                      match o with
                                      |MapValue f.Name v -> f.SetValue(r, this.ReadInstance(this.TypeIfInfoRequired(f.FieldType), this.CanBeCached(this.TypeIfInfoRequired(f.FieldType)), v))
                                      |_                 -> raise(InvalidOperationException())

                                  marshaller.Unmarshal(r)



                                  
                                
