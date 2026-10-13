module SnapshotTests

open System
open System.IO
open Xunit
open Snapshot

/// 玩具值：一个整数列表，编码为逗号分隔文本。
let enc (xs: int list) = xs |> List.map string |> String.concat ","
let dec (s: string) =
    try Ok(if s = "" then [] else s.Split ',' |> Array.map int |> List.ofArray)
    with e -> Error e.Message

let tempPath () = Path.Combine(Path.GetTempPath(), "snap-" + Guid.NewGuid().ToString("N"), "s.snap")

// SPEC 1：往返与覆盖
[<Fact>]
let ``save then load round-trips and overwrites`` () =
    let p = tempPath ()
    Assert.Equal(Missing, Snapshot.load p dec)
    Snapshot.save p enc [ 1; 2; 3 ]
    Assert.Equal(Loaded [ 1; 2; 3 ], Snapshot.load p dec)
    Snapshot.save p enc [ 4 ]
    Assert.Equal(Loaded [ 4 ], Snapshot.load p dec)
    Assert.False(File.Exists(p + ".tmp"))

// SPEC 2：任何字节被改或截断都判为 Corrupt
[<Fact>]
let ``any flipped or truncated byte is Corrupt`` () =
    let p = tempPath ()
    Snapshot.save p enc [ 10; 20; 30 ]
    let good = File.ReadAllBytes p
    for i in 0 .. good.Length - 1 do
        let b = Array.copy good
        b[i] <- b[i] ^^^ 0x01uy
        File.WriteAllBytes(p, b)
        match Snapshot.load p dec with
        | Corrupt _ -> ()
        | r -> failwithf "byte %d flipped but got %A" i r
    for cut in 0 .. good.Length - 1 do
        File.WriteAllBytes(p, good[.. cut - 1])
        match Snapshot.load p dec with
        | Corrupt _ -> ()
        | r -> failwithf "cut %d but got %A" cut r

// SPEC 3：未完成的临时文件不影响已有快照
[<Fact>]
let ``leftover tmp file does not affect existing snapshot`` () =
    let p = tempPath ()
    Snapshot.save p enc [ 1 ]
    File.WriteAllText(p + ".tmp", "garbage")
    Assert.Equal(Loaded [ 1 ], Snapshot.load p dec)
    Snapshot.save p enc [ 2 ]
    Assert.Equal(Loaded [ 2 ], Snapshot.load p dec)
