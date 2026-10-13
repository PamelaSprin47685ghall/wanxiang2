module CommandIdTests

open System
open Xunit
open Wanxiang.Core

[<Fact>]
let ``command id matches the golden vector computed independently with Python hashlib`` () =
    let id = CommandId.compute (Guid "11111111-2222-3333-4444-555555555555") "conversation.rename" """{"a":1}"""
    Assert.Equal("WyMw23eIPDlRGgNZYpN_LuQ7028dv44jTuXrYpeOHaw", id)

[<Fact>]
let ``sha256Hex matches the golden vector for UTF-8 text`` () =
    Assert.Equal("af971d05078ad95eae5d0d14d9e7c2392e0f86e44d48289c877a94bcbe06e652", CommandId.sha256Hex "万象")

[<Fact>]
let ``retry with the same invocation gives the same id; any changed part gives another`` () =
    let inv = Guid.NewGuid()
    let id = CommandId.compute inv "t" "p"
    Assert.Equal(id, CommandId.compute inv "t" "p")
    Assert.NotEqual<string>(id, CommandId.compute (Guid.NewGuid()) "t" "p")
    Assert.NotEqual<string>(id, CommandId.compute inv "u" "p")
    Assert.NotEqual<string>(id, CommandId.compute inv "t" "q")

[<Fact>]
let ``command id is unpadded base64url of 43 chars`` () =
    let id = CommandId.compute (Guid.NewGuid()) "t" "p"
    Assert.Equal(43, id.Length)
    Assert.All(id.ToCharArray(), fun c -> Assert.True(Char.IsAsciiLetterOrDigit c || c = '-' || c = '_'))
