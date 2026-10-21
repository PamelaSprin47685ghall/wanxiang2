module DeviceSightingsTests

open System
open System.IO
open Xunit
open Wanxiang.Server

let private tempDir () =
    let d =
        Path.Combine(Path.GetTempPath(), "wx-sightings-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory d |> ignore
    d

let private t0 = DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)

[<Fact>]
let ``认证记账只写主机自己的文件，配置文件一个字节都不碰`` () =
    let dir = tempDir ()

    try
        let config = Path.Combine(dir, "wanxiang.toml")
        File.WriteAllText(config, "# user owned\n")
        let before = File.GetLastWriteTimeUtc config
        let s = DeviceSightings(Path.Combine(dir, "state", "device-sightings.json"))

        for i in 0..9 do
            Assert.Equal(Ok(), s.Seen("h1", t0.AddMinutes(float i)))

        Assert.Equal("# user owned\n", File.ReadAllText config)
        Assert.Equal(before, File.GetLastWriteTimeUtc config)

        Assert.Equal<string list>(
            [ "device-sightings.json"; "wanxiang.toml" ],
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            |> Seq.map Path.GetFileName
            |> Seq.sort
            |> List.ofSeq
        )
    finally
        Directory.Delete(dir, true)

[<Fact>]
let ``一分钟内重复认证只改内存，跨进程重开后读回已落盘的时间`` () =
    let dir = tempDir ()

    try
        let p = Path.Combine(dir, "s.json")
        let s = DeviceSightings(p)
        s.Seen("h", t0) |> ignore
        s.Seen("h", t0.AddSeconds 30.0) |> ignore
        Assert.Equal(Some(t0.AddSeconds 30.0), s.LastSeen "h")
        Assert.Equal(Some t0, DeviceSightings(p).LastSeen "h")
        s.Seen("h", t0.AddMinutes 2.0) |> ignore
        Assert.Equal(Some(t0.AddMinutes 2.0), DeviceSightings(p).LastSeen "h")
    finally
        Directory.Delete(dir, true)

[<Fact>]
let ``迁移只补缺、取较新者，无变化不写盘；损坏文件当空表`` () =
    let dir = tempDir ()

    try
        let p = Path.Combine(dir, "s.json")
        File.WriteAllText(p, "{not json")
        let s = DeviceSightings(p)
        Assert.Equal(None, s.LastSeen "a")
        s.Seen("a", t0.AddHours 1.0) |> ignore
        s.Seed [ "a", t0; "b", t0 ] |> ignore
        Assert.Equal(Some(t0.AddHours 1.0), s.LastSeen "a")
        Assert.Equal(Some t0, DeviceSightings(p).LastSeen "b")
        let w = File.GetLastWriteTimeUtc p
        Threading.Thread.Sleep 50
        s.Seed [ "a", t0; "b", t0 ] |> ignore
        Assert.Equal(w, File.GetLastWriteTimeUtc p)
    finally
        Directory.Delete(dir, true)
