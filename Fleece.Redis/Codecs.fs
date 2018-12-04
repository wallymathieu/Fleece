namespace Fleece.Redis
open FSharpPlus
open FSharpPlus.Data
open StackExchange.Redis
open System.Collections.Generic
open System

[<AutoOpen>]
module Redis=
  module Helpers =
    /// try to find entry with name equal to key
    let inline tryFindEntry (key:string) (o: HashEntry list) =
      let k :RedisValue= implicit key
      o |> List.tryFind (fun p -> p.Name.Equals(k) ) 
        |> Option.map (fun v-> v.Value)
    module HashEntryList=
      let union a b = List.append a b // NOTE: let's start with this, need to verify assumption later
  open Helpers

  type RType =
      | Object = 1
      | Array  = 2
      | Number = 3
      | String = 4
      | Bool   = 5

  type DecodeError =
      | RedisTypeMismatch of System.Type * RedisValue * RType * RType
      | NullString of System.Type
      | IndexOutOfRange of int * RedisValue
      | InvalidValue of System.Type * RedisValue * string
      | PropertyNotFound of string * (HashEntry list)
      | ParseError of System.Type * exn * string
      | Uncategorized of string
      | Multiple of DecodeError list

  with
      static member (+) (x, y) =
          match x, y with
          | Multiple x, Multiple y -> Multiple (x @ y)
          | _                      -> Multiple [x; y]
      override x.ToString () =
          match x with
          | RedisTypeMismatch (t, v: RedisValue, expected, actual) -> sprintf "%s expected but got %s while decoding %s as %s" (string expected) (string actual) (string v) (string t)
          | NullString t -> sprintf "Expected %s, got null" (string t)
          | IndexOutOfRange (e, a) -> sprintf "Expected array with %s items, was: %s" (string e) (string a)
          | InvalidValue (t, v, s) -> sprintf "Value %s is invalid for %s%s" (string v) (string t) (if String.IsNullOrEmpty s then "" else " " + s)
          | PropertyNotFound (p, o) -> sprintf "Property: '%s' not found in object '%s'" p (string o)
          | ParseError (t, s, v) -> sprintf "Error decoding %s from  %s: %s" (string v) (string t) (string s)
          | Uncategorized str -> str
          | Multiple lst -> List.map string lst |> String.concat "\r\n"

  type 'a ParseResult = Result<'a, DecodeError>

  module Decode =
      let inline Success x = Ok x
      let (|Success|Failure|) = function
          | Ok    x -> Success x
          | Error x -> Failure x

      module Fail =
          let [<GeneralizableValue>]nullString<'t> : Result<'t, _> = Error (NullString typeof<'t>)
          let inline count e a = Error (IndexOutOfRange (e, a))
          let invalidValue v o : Result<'t, _> = Error (InvalidValue (typeof<'t>, v, o))
          let propertyNotFound p o = Error (PropertyNotFound (p, o))
          let parseError s v : Result<'t, _> = Error (ParseError (typeof<'t>, s, v))

  /// Encodes a value of a generic type 't into a value of raw type 'S.
  type Encoder<'S, 't> = 't -> 'S

  /// Decodes a value of raw type 'S into a value of generic type 't, possibly returning an error.
  type Decoder<'S, 't> = 'S -> ParseResult<'t>

  /// A decoder from raw type 'S1 and encoder to raw type 'S2 for string types 't1 and 't2.
  type Codec<'S1, 'S2, 't1, 't2> = Decoder<'S1, 't1> * Encoder<'S2, 't2>

  /// A decoder from raw type 'S1 and encoder to raw type 'S2 for type 't.
  type Codec<'S1, 'S2, 't> = Codec<'S1, 'S2, 't, 't>

  /// A codec for raw type 'S decoding to strong type 't1 and encoding to strong type 't2.
  type SplitCodec<'S, 't1, 't2> = Codec<'S, 'S, 't1, 't2>

  /// A codec for raw type 'S to strong type 't.
  type Codec<'S, 't> = Codec<'S, 'S, 't>

  type ConcreteCodec<'S1, 'S2, 't1, 't2> = { Decoder : ReaderT<'S1, ParseResult<'t1>>; Encoder : 't2 -> Const<'S2, unit> } with
      static member inline Return f = { Decoder = result f; Encoder = konst <| result () }
      static member inline (<*>) (remainderFields: ConcreteCodec<'S, 'S, 'f ->'r, 'T>, currentField: ConcreteCodec<'S, 'S, 'f, 'T>) =
          {
              Decoder = (remainderFields.Decoder : ReaderT<'S, ParseResult<'f -> 'r>>) <*> currentField.Decoder
              Encoder = fun w -> (remainderFields.Encoder w *> currentField.Encoder w)
          }
      static member inline (<!>) (f, field: ConcreteCodec<'S, 'S, 'f, 'T>) = f <!> field
      static member inline (<|>) (source: ConcreteCodec<'S, 'S, 'f, 'T>, alternative: ConcreteCodec<'S, 'S, 'f, 'T>) =
          {
              Decoder = (source.Decoder : ReaderT<'S, ParseResult<'f>>) <|> alternative.Decoder
              Encoder = fun w -> (source.Encoder w ++ alternative.Encoder w)
          }

  module Codec =
    /// Turns a Codec into another Codec, by mapping it over an isomorphism.
    let inline invmap (f: 'T -> 'U) (g: 'U -> 'T) (r, w) = (contramap f r, map g w)

    let inline compose codec1 codec2 = 
        let (dec1, enc1) = codec1
        let (dec2, enc2) = codec2
        (dec1 >> (=<<) dec2, enc1 << enc2)

    let decode (d: Decoder<'i, 'a>, _) (i: 'i) : ParseResult<'a> = d i
    let encode (_, e: Encoder<'o, 'a>) (a: 'a) : 'o = e a

    let inline toMonoid x = x |> toList
    let inline ofMonoid (x:KeyValuePair<_,_> list) = x |> (List.map (fun kv->HashEntry(implicit kv.Key,implicit kv.Value)))

    let inline ofConcrete {Decoder = ReaderT d; Encoder = e} = contramap toMonoid d, map ofMonoid (e >> Const.run)
    let inline toConcrete (d: _ -> _, e: _ -> _) = { Decoder = ReaderT (contramap ofMonoid d); Encoder = Const << map toMonoid e }

  /// <summary>Initialize the field mappings.</summary>
  /// <param name="f">An object constructor as a curried function.</param>
  /// <returns>The resulting object codec.</returns>
  let withFields f = (fun _ -> Success f), (fun _ -> [])

  let diApply combiner (remainderFields: SplitCodec<'S, 'f ->'r, 'T>) (currentField: SplitCodec<'S, 'f, 'T>) =
      ( 
          Compose.run (Compose (fst remainderFields: Decoder<'S, 'f -> 'r>) <*> Compose (fst currentField)),
          fun p -> combiner (snd remainderFields p) ((snd currentField) p)
      )
  /// Gets a value from a Json object
  let inline rgetWith ofRedis (o: HashEntry list) key =
      match tryFindEntry key o with
      | Some value -> ofRedis value
      | _ -> Decode.Fail.propertyNotFound key o

  /// <summary>Appends a field mapping to the codec.</summary>
  /// <param name="codec">The codec to be used.</param>
  /// <param name="fieldName">A string that will be used as key to the field.</param>
  /// <param name="getter">The field getter function.</param>
  /// <param name="rest">The other mappings.</param>
  /// <returns>The resulting object codec.</returns>
  let inline rfieldWith codec fieldName (getter: 'T -> 'Value) (rest: SplitCodec<_, _ -> 'Rest, _>) =
      let inline deriveFieldCodec codec prop getter =
          (
              (fun (o: HashEntry list) -> rgetWith (fst codec) o prop),
              (getter >> fun (x: 'Value) -> [HashEntry(implicit prop, implicit ((snd codec) x))])
          )
      diApply HashEntryList.union rest (deriveFieldCodec codec fieldName getter)

  // Tries to get a value from a Json object.
  /// Returns None if key is not present in the object.
  let inline rgetOptWith ofRedis (o: HashEntry list) key =
      match tryFindEntry key o with
      | Some value -> ofRedis value |> map Some
      | _ -> Success None
