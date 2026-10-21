module SnapshotTests

open System
open System.IO
open Xunit
open Snapshot

let tempPath () =
    Path.Combine(Path.GetTempPath(), "snap-" + Guid.NewGuid().ToString("N"), "s.snap")

// SPEC 1：往返与覆盖
[<Fact>]
let ``save then load round-trips and overwrites`` () =
    let p = tempPath ()
    Assert.Equal(Missing, SnapshotFile(p).Load())
    SnapshotFile(p).Save "1,2,3"
    Assert.Equal(Loaded "1,2,3", SnapshotFile(p).Load())
    SnapshotFile(p).Save "四"
    Assert.Equal(Loaded "四", SnapshotFile(p).Load())
    Assert.False(File.Exists(p + ".tmp"))

// SPEC 2：任何字节被改或截断都判为 Corrupt
[<Fact>]
let ``any flipped or truncated byte is Corrupt`` () =
    let p = tempPath ()
    SnapshotFile(p).Save "10,20,30"
    let good = File.ReadAllBytes p

    for i in 0 .. good.Length - 1 do
        let b = Array.copy good
        b[i] <- b[i] ^^^ 0x01uy
        File.WriteAllBytes(p, b)

        match SnapshotFile(p).Load() with
        | Corrupt _ -> ()
        | r -> failwithf "byte %d flipped but got %A" i r

    for cut in 0 .. good.Length - 1 do
        File.WriteAllBytes(p, good[.. cut - 1])

        match SnapshotFile(p).Load() with
        | Corrupt _ -> ()
        | r -> failwithf "cut %d but got %A" cut r

// SPEC 3：未完成的临时文件不影响已有快照
[<Fact>]
let ``leftover tmp file does not affect existing snapshot`` () =
    let p = tempPath ()
    SnapshotFile(p).Save "1"
    File.WriteAllText(p + ".tmp", "garbage")
    Assert.Equal(Loaded "1", SnapshotFile(p).Load())
    SnapshotFile(p).Save "2"
    Assert.Equal(Loaded "2", SnapshotFile(p).Load())

// SPEC 4：头里有非法 UTF-8 判为 Corrupt，并指明是头
[<Fact>]
let ``invalid utf8 in header is Corrupt`` () =
    let p = tempPath ()
    SnapshotFile(p).Save "x"
    let b = File.ReadAllBytes p
    b[0] <- 0xFFuy
    File.WriteAllBytes(p, b)

    match SnapshotFile(p).Load() with
    | Corrupt why -> Assert.Contains("header", why)
    | r -> failwithf "%A" r

// SPEC 7：状态有主人
[<Fact>]
let ``instances are isolated and a corrupt file is replaced by the next save`` () =
    let pa, pb = tempPath (), tempPath ()
    let a, b = SnapshotFile(pa), SnapshotFile(pb)
    a.Save "A1"
    // 实例隔离：b 只管自己的路径，a 的写入不改变 b 的答案
    Assert.Equal(Missing, b.Load())
    Assert.False(File.Exists pb)
    Assert.Equal(Loaded "A1", a.Load())
    // 错误之后状态合法：文件被外部弄坏，Load 如实报 Corrupt，下一次 Save 覆盖它
    File.WriteAllBytes(pa, [| 0uy |])

    match a.Load() with
    | Corrupt _ -> ()
    | r -> failwithf "%A" r

    a.Save "A2"
    Assert.Equal(Loaded "A2", a.Load())
    Assert.False(File.Exists(pa + ".tmp"))
    // 读出的文本是副本，不是内部状态的活视图：之后保存别的值不改动已经拿到的结果
    let got = a.Load()
    a.Save "A3"
    Assert.Equal(Loaded "A2", got)
