module JsonSeqTests

open System
open System.IO
open Xunit
open JsonSeq

let tempFile () = Path.Combine(Path.GetTempPath(), "jsonseq-" + Guid.NewGuid().ToString("N"), "log.jsonseq")

// SPEC 1：往返
[<Fact>]
let ``encode then split round-trips text and offsets`` () =
    let texts = [ """{"a":1}"""; """{"b":"汉字"}"""; "{}" ]
    let bytes = texts |> List.map Frame.encode |> Array.concat
    let rs = Frame.split 0L (ReadOnlySpan bytes)
    Assert.Equal<string list>(texts, rs |> List.map _.payload)
    Assert.True(rs |> List.forall _.complete)
    Assert.Equal(int64 bytes.Length, (List.last rs).endOffset)

// SPEC 2：任意位置截断，前面的完整记录不受影响，尾部至多一条不完整
[<Fact>]
let ``truncation at every byte yields complete prefix plus at most one incomplete tail`` () =
    let texts = [ for i in 0..4 -> sprintf """{"i":%d,"s":"%s"}""" i (String('x', i)) ]
    let bytes = texts |> List.map Frame.encode |> Array.concat
    for cut in 0 .. bytes.Length do
        let rs = Frame.split 0L (ReadOnlySpan(bytes, 0, cut))
        let complete = rs |> List.takeWhile _.complete
        Assert.Equal<string list>(texts |> List.take complete.Length, complete |> List.map _.payload)
        Assert.True(rs.Length - complete.Length <= 1)

// SPEC 3：防呆
[<Fact>]
let ``encode rejects RS inside payload`` () =
    Assert.Throws<ArgumentException>(fun () -> Frame.encode "a\u001eb" |> ignore) |> ignore

// SPEC 4：文件追加、重开、截断
[<Fact>]
let ``file append survives reopen and truncate rolls back`` () =
    let p = tempFile ()
    let mid =
        use f = new JsonSeqFile(p)
        let m = f.Append """{"n":1}"""
        f.Append """{"n":2}""" |> ignore
        m
    use f = new JsonSeqFile(p)
    Assert.Equal(2, f.ReadFrom(0L).Length)
    Assert.Equal<string list>([ """{"n":2}""" ], f.ReadFrom mid |> List.map _.payload)
    f.Truncate mid
    Assert.Equal(1, f.ReadFrom(0L).Length)
