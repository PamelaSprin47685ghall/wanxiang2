module Tests

open System
open System.IO
open Xunit
open Wanxiang.Store

[<Fact>]
let ``复制并核对哈希，不覆盖已有副本`` () =
    let d = Path.Combine(Path.GetTempPath(), "wx-vc-" + Guid.NewGuid().ToString("N"))
    let t = Path.Combine(d, "t")
    Directory.CreateDirectory t |> ignore
    try
        let f = Path.Combine(d, "a.log")
        File.WriteAllText(f, "abc")
        let sha, size = VerifiedCopy.copyInto t f
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", sha)
        Assert.Equal(3L, size)
        Assert.Equal("abc", File.ReadAllText(Path.Combine(t, "a.log")))
        Assert.ThrowsAny<IOException>(fun () -> VerifiedCopy.copyInto t f |> ignore) |> ignore
    finally Directory.Delete(d, true)
