module MediaSniffTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``magic numbers win; printable bytes are text; control bytes are unknown`` () =
    Assert.Equal(Some "image/png", MediaSniff.sniff [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0uy |])
    Assert.Equal(Some "image/jpeg", MediaSniff.sniff [| 0xFFuy; 0xD8uy; 0xFFuy |])
    Assert.Equal(Some "application/pdf", MediaSniff.sniff "%PDF-1"B)
    Assert.Equal(Some "text/plain", MediaSniff.sniff "hi\n\tthere"B)
    Assert.Equal(None, MediaSniff.sniff [| 1uy; 2uy |])

[<Fact>]
let ``declared type is kept unless empty or octet-stream`` () =
    Assert.Equal("image/webp", MediaSniff.resolve "image/webp" (Some "image/png"))
    Assert.Equal("image/png", MediaSniff.resolve "application/octet-stream" (Some "image/png"))
    Assert.Equal("application/octet-stream", MediaSniff.resolve "" None)
    Assert.Equal("application/octet-stream", MediaSniff.resolve "application/octet-stream" None)

[<Fact>]
let ``file names lose control characters, are capped and trimmed`` () =
    Assert.Equal("a b.txt", MediaSniff.sanitizeFileName "  a\u0001 b.txt\n ")
    Assert.Equal(255, (MediaSniff.sanitizeFileName (String.replicate 300 "x")).Length)
