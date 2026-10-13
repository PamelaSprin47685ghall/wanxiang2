module SnapshotTests

open System
open System.IO
open Xunit
open Snapshot

let tempPath () = Path.Combine(Path.GetTempPath(), "snap-" + Guid.NewGuid().ToString("N"), "s.snap")

// SPEC 1：往返与覆盖
[<Fact>]
let ``save then load round-trips and overwrites`` () =
    let p = tempPath ()
    Assert.Equal(Missing, Snapshot.load p)
    Snapshot.save p "1,2,3"
    Assert.Equal(Loaded "1,2,3", Snapshot.load p)
    Snapshot.save p "四"
    Assert.Equal(Loaded "四", Snapshot.load p)
    Assert.False(File.Exists(p + ".tmp"))

// SPEC 2：任何字节被改或截断都判为 Corrupt
[<Fact>]
let ``any flipped or truncated byte is Corrupt`` () =
    let p = tempPath ()
    Snapshot.save p "10,20,30"
    let good = File.ReadAllBytes p
    for i in 0 .. good.Length - 1 do
        let b = Array.copy good
        b[i] <- b[i] ^^^ 0x01uy
        File.WriteAllBytes(p, b)
        match Snapshot.load p with
        | Corrupt _ -> ()
        | r -> failwithf "byte %d flipped but got %A" i r
    for cut in 0 .. good.Length - 1 do
        File.WriteAllBytes(p, good[.. cut - 1])
        match Snapshot.load p with
        | Corrupt _ -> ()
        | r -> failwithf "cut %d but got %A" cut r

// SPEC 3：未完成的临时文件不影响已有快照
[<Fact>]
let ``leftover tmp file does not affect existing snapshot`` () =
    let p = tempPath ()
    Snapshot.save p "1"
    File.WriteAllText(p + ".tmp", "garbage")
    Assert.Equal(Loaded "1", Snapshot.load p)
    Snapshot.save p "2"
    Assert.Equal(Loaded "2", Snapshot.load p)
