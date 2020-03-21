using System;
using FSharpPlusCSharp;
using Microsoft.FSharp.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CSharpTests
{
    
    public struct Item {
        public Item(int id, string brand, string availability)
        {
            Id = id;
            Brand = brand;
            Availability = availability;
        }

        public int Id{ get; }
        public string Brand{ get; }
        public string Availability { get; }

        public static FSharpResult<Item, Fleece.Newtonsoft.DecodeError> FromJSON(Item _, JToken token)
        {
            var x = Fleece.Newtonsoft.Operators.jreq<Item,FSharpOption<string>>("id", (v => Options.Some(v.Id)));
            //var x = from .  

            /*
             *  <!> jreq  "id"          (fun x -> Some x.Id     )
        <*> jreq  "brand"       (fun x -> Some x.Brand  )
        <*> jopt "availability" (fun x -> x.Availability)
             * 
             */
        }    
        /*
    static member FromJSON (_: Attribute) =
        function
        | JObject o -> 
            monad {
                let! name = o .@ "name"
                if name = null then 
                    return! Decode.Fail.nullString
                else
                    let! value = o .@ "value"
                    return {
                        Attribute.Name = name
                        Value = value
                    }
            }
        | x -> Decode.Fail.objExpected x
         */
        
        /*
    static member JsonObjCodec =
        fun id brand availability -> { Item.Id = id; Brand = brand; Availability = availability }
        <!> jreq  "id"          (fun x -> Some x.Id     )
        <*> jreq  "brand"       (fun x -> Some x.Brand  )
        <*> jopt "availability" (fun x -> x.Availability)
        |> Codec.ofConcrete
     
     


    static member ToJson (x: Attribute) =
        jobj [ "name" .= x.Name; "value" .= x.Value ]
         *
         */
     
            //-> Item ParseResult * Item -> IReadOnlyDictionary<string,JsonValue>

        //public static 
    }

    public class FromJSONTests
    {
        public void Item_with_missing_key()
        {
            var actual = Fleece.Newtonsoft.parseJson<Item>(@"{""id"": 1, ""brand"": ""Sony""}");
            var expected = 
                new Item(id:1,brand:"Sony",availability:null);

            Assert.Equal(actual, Results.Ok<Item, Fleece.Newtonsoft.DecodeError>(expected));
        }
        
        
        /*
         *

            test "nested item" {
                let actual: NestedItem ParseResult = parseJson """{"id": 1, "blah": {"brand": "Sony", "availability": "1 week"}}"""
                let expected = 
                    NestedItem {
                        Item.Id = 1
                        Brand = "Sony"
                        Availability = Some "1 week"
                    }
                Assert.Equal("item", Success expected, actual)
            }

            test "attribute ok" {
                let actual : Attribute ParseResult = parseJson """{"name": "a name", "value": "a value"}"""
                let expected = 
                    { Attribute.Name = "a name"
                      Value = "a value" }
                Assert.Equal("attribute", Success expected, actual)
            }

            test "attribute with null name" {
                let actual : Attribute ParseResult = parseJson """{"name": null, "value": "a value"}"""
                match actual with
                | Success a -> failtest "should have failed"
                | Failure e -> ()
            }

            test "attribute with null value" {
                let actual : Attribute ParseResult = parseJson """{"name": "a name", "value": null}"""
                let expected = 
                    { Attribute.Name = "a name"
                      Value = null }
                Assert.Equal("attribute", Success expected, actual)           
            }

            test "Person recursive" {
                let actual : Person ParseResult = parseJson """{"name": "John", "age": 44, "gender": "Male", "children": [{"name": "Katy", "age": 5, "gender": "Female", "children": []}, {"name": "Johnny", "age": 7, "gender": "Male", "children": []}]}"""
                let expectedPerson = 
                    { Person.Name = "John"
                      Age = 44
                      Gender = Gender.Male
                      Children = 
                      [
                        { Person.Name = "Katy"
                          Age = 5
                          Gender = Gender.Female
                          Children = [] }
                        { Person.Name = "Johnny"
                          Age = 7
                          Gender = Gender.Male
                          Children = [] }
                      ] }
                Assert.Equal("Person", Success expectedPerson, actual)
            }
         */
        
        [Fact]
        public void Test1()
        {

        }
    }
}
