module Tests.Tests
open System
open System.Text
open System.Collections.Generic
open System.Linq
open Fuchu
open Fleece.Redis
open Fleece.Redis.Operators
open FSharpPlus
type Person = {
    Name: string
    Age: int
    Children: Person list
}

type Person with
    static member Create name age children = { Person.Name = name; Age = age; Children = children }

    static member OfJson json = 
        match json with
        | RObject o -> Person.Create <!> (o .@ "name") <*> (o .@ "age") <*> (o .@ "children")
        | x -> Decode.Fail.objExpected x

    static member ToJson (x: Person) =
        robj [ 
            "name" .= x.Name
            "age" .= x.Age
            "children" .= x.Children
        ]

type Item = {
    Id: int
    Brand: string
    Availability: string option
}

type Item with
    static member RedisObjCodec =
        fun id brand availability -> { Item.Id = id; Brand = brand; Availability = availability }
        <!> rreq  "id"          (fun x -> Some x.Id     )
        <*> rreq  "brand"       (fun x -> Some x.Brand  )
        <*> ropt "availability" (fun x -> x.Availability)
        |> Codec.ofConcrete

type NestedItem = NestedItem of Item

type NestedItem with
    static member OfJson json =
        match json with
        | RObject o ->
            monad {
                let! id = o .@ "id"
                let! sub = o .@ "blah" |> map jsonObjectGetValues
                let! brand = sub .@ "brand"
                let! availability = sub .@? "availability"
                return NestedItem {
                    Item.Id = id
                    Brand = brand
                    Availability = availability
                }
            }
        | x -> Decode.Fail.objExpected x


let tag prop codec =
    Codec.ofConcrete codec
    |> Codec.compose (
                        (fun o -> match Seq.toList o with [KeyValue(p, RObject a)] when p = prop -> Ok a | _ -> Decode.Fail.propertyNotFound prop o), 
                        (fun x -> if Seq.isEmpty x then zero else Dict.toIReadOnlyDictionary (dict [prop, JObject x]))
                     )
    |> Codec.toConcrete

type Vehicle =
   | Bike
   | MotorBike of unit
   | Car       of make : string
   | Van       of make : string * capacity : float
   | Truck     of make : string * capacity : float
   | Aircraft  of make : string * capacity : float
with
    static member JsonObjCodec =
        [
            (fun () -> Bike) <!> rreq "bike"      (function  Bike             -> Some ()     | _ -> None)
            MotorBike        <!> rreq "motorBike" (function (MotorBike ()   ) -> Some ()     | _ -> None)
            Car              <!> rreq "car"       (function (Car  x         ) -> Some  x     | _ -> None)
            Van              <!> rreq "van"       (function (Van (x, y)     ) -> Some (x, y) | _ -> None)
            tag "truck" (
                (fun m c -> Truck (make = m, capacity = c))
                    <!> rreq  "make"     (function (Truck (make     = x)) -> Some  x | _ -> None)
                    <*> rreq  "capacity" (function (Truck (capacity = x)) -> Some  x | _ -> None))
            tag "aircraft" (
                (fun m c -> Aircraft (make = m, capacity = c))
                    <!> rreq  "make"     (function (Aircraft (make     = x)) -> Some  x | _ -> None)
                    <*> rreq  "capacity" (function (Aircraft (capacity = x)) -> Some  x | _ -> None))
        ] |> rchoice

type Name = {FirstName: string; LastName: string} with
    static member ToRedis x = toRedis (x.LastName + ", " + x.FirstName)
    static member OfRedis x =
        match x with
        | RString x when String.contains ',' x -> Ok { FirstName = (split [|","|] x).[0]; LastName = (split [|","|] x).[1] }
        | RString _ -> Error "Expected a ',' separator"
        | _ -> Error "Invalid Json Type"
        
let strCleanUp x = System.Text.RegularExpressions.Regex.Replace(x, @"\s|\r\n?|\n", "")
let strCleanUpAll x = System.Text.RegularExpressions.Regex.Replace(x, "\s|\r\n?|\n|\"|\\\\", "")
type Assert with
    static member inline JSON(expected: string, value: 'a) =
        Assert.Equal("", expected, strCleanUp ((toRedis value).ToString()))


open FsCheck
open FsCheck.GenOperators