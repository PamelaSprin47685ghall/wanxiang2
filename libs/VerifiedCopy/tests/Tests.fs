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

        Assert.ThrowsAny<IOException>(fun () -> VerifiedCopy.copyInto t f |> ignore)
        |> ignore
    finally
        Directory.Delete(d, true)

let private withDir f =
    let d = Path.Combine(Path.GetTempPath(), "wx-vc-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore

    try
        f d
    finally
        Directory.Delete(d, true)

[<Fact>]
let ``空文件的哈希是 SHA-256 空串`` () =
    withDir (fun d ->
        let f = Path.Combine(d, "e")
        File.WriteAllBytes(f, [||])
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", VerifiedCopy.sha256OfFile f))

[<Fact>]
let ``大于缓冲区的文件逐块哈希仍正确`` () =
    withDir (fun d ->
        let f = Path.Combine(d, "big")
        let bytes = Array.init (200 * 1024 + 7) (fun i -> byte (i % 251))
        File.WriteAllBytes(f, bytes)

        let expected =
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData bytes)

        Assert.Equal(expected, VerifiedCopy.sha256OfFile f))

[<Fact>]
let ``原件不存在时抛错且不留副本`` () =
    withDir (fun d ->
        Assert.ThrowsAny<IOException>(fun () -> VerifiedCopy.copyInto d (Path.Combine(d, "nope", "x")) |> ignore)
        |> ignore

        Assert.Empty(Directory.GetFiles d))

[<Fact>]
let ``目标目录不存在时抛错`` () =
    withDir (fun d ->
        let f = Path.Combine(d, "a")
        File.WriteAllText(f, "x")

        Assert.ThrowsAny<IOException>(fun () -> VerifiedCopy.copyInto (Path.Combine(d, "missing")) f |> ignore)
        |> ignore)

[<Fact>]
let ``写入者持有原件时仍可共享读取`` () =
    withDir (fun d ->
        let f = Path.Combine(d, "live.log")
        use w = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)
        w.Write([| 1uy; 2uy |], 0, 2)
        w.Flush()
        Assert.Equal(64, (VerifiedCopy.sha256OfFile f).Length))
