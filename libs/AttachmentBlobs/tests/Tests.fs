module AttachmentBlobsTests

open System
open System.IO
open System.Security.Cryptography
open Xunit
open Wanxiang.Server

let root () = Path.Combine(Path.GetTempPath(), "wx-blobs-" + Guid.NewGuid().ToString("N"))
let hex (b: byte[]) = Convert.ToHexString(SHA256.HashData b).ToLowerInvariant()

[<Fact>]
let ``chunks in order complete to a content-addressed file stored once`` () =
    let r = root ()
    let b = AttachmentBlobs(r, 100L, 4)
    let data = "abcdefg"B
    let h = hex data
    for _ in 1..2 do
        let id = Guid.NewGuid()
        Assert.Equal(Ok(), b.Begin(1, id, 7L, h.ToUpperInvariant()))
        Assert.Equal(Ok(), b.Append(id, 0, Convert.ToBase64String data[0..3]))
        Assert.Equal(Ok(), b.Append(id, 1, Convert.ToBase64String data[4..]))
        match b.Complete(id, h) with
        | Ok(sha, size, path) ->
            Assert.Equal((h, 7L), (sha, size))
            Assert.Equal(Path.Combine(r, h[0..1], h[2..3], h), path)
        | Error e -> failwithf "%A" e
    Assert.True(b.Exists h)
    Assert.Empty(Directory.GetFiles(Path.Combine(r, ".tmp")))

[<Fact>]
let ``bad input is refused with the old texts`` () =
    let b = AttachmentBlobs(root (), 10L, 4)
    let id = Guid.NewGuid()
    let h = hex "abc"B
    Assert.Equal(Error(Invalid "attachment size must not be negative"), b.Begin(1, id, -1L, h))
    Assert.Equal(Error(Invalid "attachment sha256 must be 64 hexadecimal characters"), b.Begin(1, id, 1L, "x"))
    Assert.Equal(Error(TooLarge(10L, 11L)), b.Begin(1, id, 11L, h))
    Assert.Equal(Ok(), b.Begin(1, id, 3L, h))
    Assert.Equal(Error(Invalid "attachment id is already active"), b.Begin(1, id, 3L, h))
    Assert.Equal(Error(Invalid "attachment chunk index 1 out of order; expected 0"), b.Append(id, 1, "YQ=="))
    Assert.Equal(Error(Invalid "attachment chunk exceeds 4 bytes limit"), b.Append(id, 0, Convert.ToBase64String "abcde"B))
    Assert.Equal(Error(Invalid "attachment chunk exceeds declared size"), b.Append(id, 0, Convert.ToBase64String "abcd"B))
    Assert.Equal(Ok(), b.Append(id, 0, "YWI="))
    Assert.Equal(Error(Invalid "attachment size mismatch: expected 3 got 2"), b.Complete(id, h))
    Assert.Equal(Error(NotActive id), b.Complete(id, h))

[<Fact>]
let ``wrong content or wrong declared hash fails and leaves nothing`` () =
    let r = root ()
    let b = AttachmentBlobs(r, 10L, 4)
    let h = hex "abc"B
    let id = Guid.NewGuid()
    b.Begin(1, id, 3L, h) |> ignore
    b.Append(id, 0, Convert.ToBase64String "abd"B) |> ignore
    Assert.Equal(Error(HashMismatch(h, hex "abd"B)), b.Complete(id, h))
    let id2 = Guid.NewGuid()
    b.Begin(1, id2, 3L, h) |> ignore
    b.Append(id2, 0, Convert.ToBase64String "abc"B) |> ignore
    Assert.Equal(Error(HashMismatch(h, "f" + h[1..])), b.Complete(id2, "f" + h[1..]))
    Assert.False(b.Exists h)
    Assert.Empty(Directory.GetFiles(Path.Combine(r, ".tmp")))

[<Fact>]
let ``abort by owner removes only that owner's uploads`` () =
    let r = root ()
    let b = AttachmentBlobs(r, 10L, 4)
    let a, c = Guid.NewGuid(), Guid.NewGuid()
    b.Begin(1, a, 1L, hex "a"B) |> ignore
    b.Begin(2, c, 1L, hex "a"B) |> ignore
    b.AbortByOwner 1
    Assert.Equal(Error(NotActive a), b.Append(a, 0, "YQ=="))
    Assert.Equal(Ok(), b.Append(c, 0, "YQ=="))
    Assert.Single(Directory.GetFiles(Path.Combine(r, ".tmp"))) |> ignore
