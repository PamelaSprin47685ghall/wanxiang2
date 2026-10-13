module JsonSeqTests

open System
open System.IO
open Xunit
open JsonSeq

let appended = function Appended e -> e | o -> failwithf "%A" o

let tempFile () = Path.Combine(Path.GetTempPath(), "jsonseq-" + Guid.NewGuid().ToString("N"), "log.jsonseq")

// SPEC 1：往返
[<Fact>]
let ``encode then split round-trips text and offsets`` () =
    let texts = [ """{"a":1}"""; """{"b":"汉字"}"""; "{}" ]
    let bytes = texts |> List.map Frame.encode |> Array.concat
    let rs = Frame.split 0L (ReadOnlySpan bytes)
    Assert.Equal<string list>(texts, rs |> List.map _.payload)
    Assert.True(rs |> List.forall (fun r -> r.fault.IsNone))
    Assert.Equal(int64 bytes.Length, (List.last rs).endOffset)

// SPEC 2：任意位置截断，前面的完整记录不受影响，尾部至多一条不完整
[<Fact>]
let ``truncation at every byte yields complete prefix plus at most one incomplete tail`` () =
    let texts = [ for i in 0..4 -> sprintf """{"i":%d,"s":"%s"}""" i (String('x', i)) ]
    let bytes = texts |> List.map Frame.encode |> Array.concat
    for cut in 0 .. bytes.Length do
        let rs = Frame.split 0L (ReadOnlySpan(bytes, 0, cut))
        let complete = rs |> List.takeWhile (fun r -> r.fault.IsNone)
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
        let m = appended (f.Append """{"n":1}""")
        appended (f.Append """{"n":2}""") |> ignore
        m
    use f = new JsonSeqFile(p)
    Assert.Equal(2, f.ReadFrom(0L).Length)
    Assert.Equal<string list>([ """{"n":2}""" ], f.ReadFrom mid |> List.map _.payload)
    f.Truncate mid
    Assert.Equal(1, f.ReadFrom(0L).Length)

// SPEC 5：非法 UTF-8 报告为 InvalidUtf8，不替换
[<Fact>]
let ``invalid utf8 is reported not replaced`` () =
    let bytes = Array.concat [ Frame.encode "{}"; [| Frame.RS; 0xC3uy; 0x28uy; Frame.LF |]; Frame.encode "{}" ]
    let rs = Frame.split 0L (ReadOnlySpan bytes)
    Assert.Equal<string option list>([ None; Some Frame.InvalidUtf8; None ], rs |> List.map _.fault)

// SPEC 6：第一个 RS 之前的字节、完全没有 RS 的文件，都报告 StrayBytes
[<Theory>]
[<InlineData("garbage")>]
[<InlineData("garbage\u001e{}\n")>]
let ``bytes before first RS are stray`` (s: string) =
    let bytes = Text.Encoding.UTF8.GetBytes(Text.RegularExpressions.Regex.Unescape s)
    let rs = Frame.split 0L (ReadOnlySpan bytes)
    Assert.Equal(Some Frame.StrayBytes, rs.Head.fault)
    Assert.Equal((0L, 7L), (rs.Head.startOffset, rs.Head.endOffset))

// SPEC 7：区间读只读出区间内的记录
[<Fact>]
let ``Read returns only records within range`` () =
    use f = new JsonSeqFile(tempFile ())
    let a = appended (f.Append "{\"n\":1}")
    let b = appended (f.Append "{\"n\":2}")
    appended (f.Append "{\"n\":3}") |> ignore
    Assert.Equal<string list>([ "{\"n\":2}" ], f.Read(a, b) |> List.map _.payload)

// SPEC 9：写之前就失败 → NotWritten；写失败且截不回 → Unknown，不冒称 NotWritten
[<Fact>]
let ``failure before writing is NotWritten`` () =
    let f = new JsonSeqFile(tempFile ())
    appended (f.Append "{}") |> ignore
    (f :> IDisposable).Dispose()
    match f.Append "{}" with NotWritten _ -> () | o -> failwithf "%A" o

[<Fact>]
let ``write failure without rollback evidence is Unknown`` () =
    // /dev/full：真实的写失败（ENOSPC），且截断同样失败。仅 Linux。
    if OperatingSystem.IsLinux() then
        use f = new JsonSeqFile("/dev/full")
        match f.Append "{}" with Unknown _ -> () | o -> failwithf "%A" o
