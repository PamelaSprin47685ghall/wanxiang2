module JsonSeqTests

open System
open System.IO
open System.Runtime.InteropServices
open Xunit
open JsonSeq

let appended = function Appended e -> e | o -> failwithf "%A" o

let tempFile () = Path.Combine(Path.GetTempPath(), "jsonseq-" + Guid.NewGuid().ToString("N"), "log.jsonseq")

/// 只在测试里用：临时收紧本进程的 RLIMIT_FSIZE（内核的文件大小上限），并忽略 SIGXFSZ。
/// 信号被忽略时，内核把超过上限的写报成 EFBIG（.NET 转成 ArgumentOutOfRangeException），
/// 而在上限内缩小文件的截回仍被允许 —— 这是与磁盘写满同类的真实故障，不是注入开关。
/// 用完恢复原上限与原信号处置。仅 Linux。
module private FsizeLimit =
    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RLimit = { mutable Cur: uint64; mutable Max: uint64 }

    [<DllImport("libc", SetLastError = true)>]
    extern int getrlimit(int resource, RLimit& rlim)

    [<DllImport("libc", SetLastError = true)>]
    extern int setrlimit(int resource, RLimit& rlim)

    [<DllImport("libc", SetLastError = true)>]
    extern IntPtr signal(int signum, IntPtr handler)

    [<Literal>]
    let private RlimitFsize = 1

    [<Literal>]
    let private Sigxfsz = 25

    /// 以文件大小上限 limit 字节运行 act，之后恢复原上限与原信号处置。
    let at (limit: int64) (act: unit -> unit) =
        let mutable old = { Cur = 0UL; Max = 0UL }
        if getrlimit (RlimitFsize, &old) <> 0 then failwith "getrlimit failed"
        let previous = signal (Sigxfsz, IntPtr 1)   // SIG_IGN
        if previous = IntPtr(-1) then failwith "signal failed"
        try
            try
                let mutable tightened = { Cur = min (uint64 limit) old.Max; Max = old.Max }
                if setrlimit (RlimitFsize, &tightened) <> 0 then failwith "setrlimit failed"
                act ()
            finally
                setrlimit (RlimitFsize, &old) |> ignore
        finally
            signal (Sigxfsz, previous) |> ignore

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

[<Fact>]
let ``write failure with rollback evidence is NotWritten`` () =
    // 真实故障：本进程 RLIMIT_FSIZE 上限（写按 EFBIG 失败），截回缩小文件仍被允许。仅 Linux。
    if OperatingSystem.IsLinux() then
        let p = tempFile ()
        use f = new JsonSeqFile(p)
        appended (f.Append "[1]") |> ignore
        let before = File.ReadAllBytes p
        // 上限卡在写前长度 + 16KB：这条 64KB 的追加必然超限；已经写进上限内的字节也须截回
        FsizeLimit.at (int64 before.Length + 16L * 1024L) (fun () ->
            match f.Append("[" + String('x', 64 * 1024) + "]") with
            | NotWritten _ -> ()
            | o -> failwithf "%A" o)
        Assert.Equal<byte[]>(before, File.ReadAllBytes p)
        Assert.Equal(int64 before.Length, f.Length)
        Assert.Equal<string list>([ "[1]" ], f.ReadFrom 0L |> List.map _.payload)

// SPEC 10：没有正文的 RS 不被忽略：末尾是 TornTail，中间是 EmptyRecord；偏移连续覆盖全部字节
[<Fact>]
let ``bare RS is a faulted record, never skipped`` () =
    let bytes = Array.concat [ Frame.encode "{}"; [| Frame.RS |]; Frame.encode "{}"; [| Frame.RS |] ]
    let rs = Frame.split 0L (ReadOnlySpan bytes)
    Assert.Equal<string option list>([ None; Some Frame.EmptyRecord; None; Some Frame.TornTail ], rs |> List.map _.fault)
    for a, b in List.pairwise rs do Assert.Equal(a.endOffset, b.startOffset)
    Assert.Equal(int64 bytes.Length, (List.last rs).endOffset)

// SPEC 11：状态有主人
[<Fact>]
let ``instances are isolated; errors and illegal arguments leave state legal`` () =
    use a = new JsonSeqFile(tempFile ())
    use b = new JsonSeqFile(tempFile ())
    let a1 = appended (a.Append """{"a":1}""")
    // 实例隔离：b 的字节与 a 的写入无关
    Assert.Equal(0L, b.Length)
    Assert.Empty(b.ReadFrom 0L)
    // 状态在文件里，不在对象里：同一路径上的第二个实例读到同一个文件（写者唯一是 DirLock 的事）
    if OperatingSystem.IsLinux() then
        use c = new JsonSeqFile(a.Path)
        Assert.Equal(a1, c.Length)
        Assert.Equal<string list>([ """{"a":1}""" ], c.ReadFrom 0L |> List.map _.payload)
    // 读出的记录不是内部状态的活视图：之后继续写、再截回，早先拿到的记录不变
    let first = a.ReadFrom 0L
    appended (a.Append """{"a":2}""") |> ignore
    a.Truncate a1
    Assert.Equal<string list>([ """{"a":1}""" ], first |> List.map _.payload)
    // 非法消息不改状态
    Assert.Throws<ArgumentException>(fun () -> a.Read(0L, a.Length + 1L) |> ignore) |> ignore
    Assert.Throws<ArgumentException>(fun () -> a.Read(-1L, 0L) |> ignore) |> ignore
    Assert.Throws<ArgumentException>(fun () -> a.Truncate -1L) |> ignore
    Assert.Throws<ArgumentException>(fun () -> a.Truncate(a.Length + 1L)) |> ignore
    Assert.Equal(a1, a.Length)
    Assert.Equal<string list>([ """{"a":1}""" ], a.ReadFrom 0L |> List.map _.payload)
    // 错误之后状态合法：继续追加、读出正常
    Assert.Equal(a1 + 9L, appended (a.Append """{"a":3}"""))
    Assert.Equal<string list>([ """{"a":1}"""; """{"a":3}""" ], a.ReadFrom 0L |> List.map _.payload)
