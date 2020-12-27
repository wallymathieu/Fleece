(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#r @"../../src/Fleece.SystemJson/bin/Release/net461/System.Json.dll"
#r @"../../src/Fleece.SystemJson/bin/Release/net461/Fleece.SystemJson.dll"
#r @"../../src/Fleece.SystemJson/bin/Release/net461/FSharpPlus.dll"

open System.Json
open Fleece.SystemJson
open Fleece.SystemJson.Operators

(**
## CODEC

For types that deserialize to Json Objets, typically (but not limited to) records, you can alternatively use codecs and have a single method which maps between fields and values. 

*)

type Person = { 
    name : string * string
    age : int option
    children: Person list } 
    with
    static member JsonObjCodec =
        fun f l a c -> { name = (f, l); age = a; children = c }
        <!> jreq  "firstName" (Some << fun x -> fst x.name)
        <*> jreq  "lastName"  (Some << fun x -> snd x.name)
        <*> jopt  "age"       (fun x -> x.age) // Optional fields: use 'jopt'
        <*> jreq  "children"  (fun x -> Some x.children)


let p = {name = ("John", "Doe"); age = None; children = [{name = ("Johnny", "Doe"); age = Some 21; children = []}]}
//printfn "%s" (string (toJson p))

let john = parseJson<Person> """{
    "children": [{
        "children": [],
        "age": 21,
        "lastName": "Doe",
        "firstName": "Johnny"
    }],
    "lastName": "Doe",
    "firstName": "John"
}"""

(**
If you prefer you can write the same with functions:
*)

type PersonF = { 
    name : string * string
    age : int option
    children: PersonF list }
    with
    static member JsonObjCodec =
        fun f l a c -> { name = (f, l); age = a; children = c }
        |> withFields
        |> jfield    "firstName" (fun x -> fst x.name)
        |> jfield    "lastName"  (fun x -> snd x.name)
        |> jfieldOpt "age"       (fun x -> x.age)
        |> jfieldWith jsonValueCodec "children"  (fun x -> x.children)

(**
Discriminated unions can be modeled with alternatives:
*)

type Shape =
    | Rectangle of width : float * length : float
    | Circle of radius : float
    | Prism of width : float * float * height : float
    with 
        static member JsonObjCodec =
            Rectangle <!> jreq "rectangle" (function Rectangle (x, y) -> Some (x, y) | _ -> None)
            <|> ( Circle <!> jreq "radius" (function Circle x -> Some x | _ -> None) )
            <|> ( Prism <!> jreq "prism"   (function Prism (x, y, z) -> Some (x, y, z) | _ -> None) )
(**
or using the jchoice combinator:
*)

type ShapeC =
    | Rectangle of width : float * length : float
    | Circle of radius : float
    | Prism of width : float * float * height : float
    with 
        static member JsonObjCodec =
            jchoice
                [
                    Rectangle <!> jreq "rectangle" (function Rectangle (x, y) -> Some (x, y) | _ -> None)
                    Circle    <!> jreq "radius"    (function Circle x -> Some x | _ -> None)
                    Prism     <!> jreq "prism"     (function Prism (x, y, z) -> Some (x, y, z) | _ -> None)
                ]

(**
What's happening here is that we're getting a Codec to/from a Json Object (not neccesarily a JsonValue) which Fleece is able to take it and fill the gap by composing it with a codec from JsonObject to/from JsonValue.

We can also do that by hand, we can manipulate codecs by using functions in the Codec module. Here's an example:
*)
open System.Text
let pf : PersonF= {name = ("John", "Doe"); age = None; children = [{name = ("Johnny", "Doe"); age = Some 21; children = []}]}

let personBytesCodec =
    let getString (bytes:byte array) = Encoding.UTF8.GetString bytes
    PersonF.JsonObjCodec
    |> Codec.compose jsonObjToValueCodec    // this is the codec that fills the gap to/from JsonValue
    |> Codec.compose jsonValueToTextCodec   // this is a codec between JsonValue and JsonText
    |> Codec.invmap getString Encoding.UTF8.GetBytes    // This is a pair of of isomorphic functions

let bytePerson = Codec.encode personBytesCodec pf
// val bytePerson : byte [] = [|123uy; 13uy; 10uy; 32uy; 32uy; ... |]
let p' = Codec.decode personBytesCodec bytePerson

(**
While if the type of codec is concrete then we need to convert it to before composing it
*)

let personBytesCodec2 =
    let getString (bytes:byte array) = Encoding.UTF8.GetString bytes
    Person.JsonObjCodec
    |> Codec.ofConcrete
    |> Codec.compose jsonObjToValueCodec    // this is the codec that fills the gap to/from JsonValue
    |> Codec.compose jsonValueToTextCodec   // this is a codec between JsonValue and JsonText
    |> Codec.invmap getString Encoding.UTF8.GetBytes    // This is a pair of of isomorphic functions


open System.Json
open Fleece.SystemJson
open Fleece.SystemJson.Operators
open System
open FSharpPlus
type Auction={id:string; at:DateTime option}
with
  static member JsonObjCodec1_0 =
    fun id -> { id =id; at = None }
    |> withFields
    |> jfield "id"      (fun x -> x.id)
    |> Codec.compose jsonObjToValueCodec
  static member JsonObjCodec1_1 =
    fun id at -> { id =id; at = at }
    |> withFields
    |> jfield "id"      (fun x -> x.id)
    |> jfield "at"      (fun x -> x.at)
    |> Codec.compose jsonObjToValueCodec

type Bid={auction:string}
with
  static member JsonObjCodec =
    fun auction -> { auction =auction; }
    |> withFields
    |> jfield "auction"      (fun x -> x.auction)

type Payload =
  | AuctionAdded of DateTime * Auction
  | BidPlaced of DateTime * Bid

  /// the time when the payload was issued
  static member getAt payload =
    match payload with
    | AuctionAdded(at, _) -> at
    | BidPlaced(at, _) -> at

  static member getAuction payload =
    match payload with
    | AuctionAdded(_,auction) -> auction.id
    | BidPlaced(_,bid) -> bid.auction
  static member OfJson_base json auctionCodecOfJson =
    match json with
    | JObject o -> monad {
        let! t = o .@ "$type"
        match t with
        | "AuctionAdded" ->
          let create d a = AuctionAdded (d,a)
          return! (create <!> (o .@ "at") <*> (jgetWith auctionCodecOfJson o "auction"))
        | "BidPlaced" ->
          let create d b = BidPlaced (d,b)
          return! (create <!> (o .@ "at") <*> (o .@ "bid"))
        | x -> return! (Decode.Fail.invalidValue json (sprintf "Expected one of: 'AddAuction', 'PlaceBid' for $type but %s" x))
      }
    | x -> Decode.Fail.objExpected json
  
  static member OfJson1_0 json =
    Payload.OfJson_base json (fst Auction.JsonObjCodec1_0)
  static member OfJson1_1 json =
    Payload.OfJson_base json (fst Auction.JsonObjCodec1_1)

  static member ToJson_base (x: Payload) auctionToJson =
    match x with
    | AuctionAdded (d,a)-> jobj [ "$type" .= "AuctionAdded"; "at" .= d; (jpairWith auctionToJson "auction" a)]
    | BidPlaced (d,b)-> jobj [ "$type" .= "BidPlaced"; "at" .= d; "bid" .= b]

  static member ToJson1_0 (x: Payload) =
    Payload.ToJson_base x (snd Auction.JsonObjCodec1_0)
  static member ToJson1_1 (x: Payload) =
    Payload.ToJson_base x (snd Auction.JsonObjCodec1_1)

type StreamId = StreamId of string
type VersionNumber = VersionNumber of string
type EventWrite<'payload, 'metadata> =
  { Id: Guid                     // ID of the event
    StreamId: StreamId           // Id of the stream this event belongs to
    VersionNumber: VersionNumber // Sequence number of the event in a single stream
    SchemaRevision: int          // Schema revision of the payload and metadata
    EventType: string            // Event Type
    Payload: 'payload            // Event data
    Timestamp: DateTime }        // Timestamp the event was generated

