module Tests

open System
open System.IO
open Xunit
open Wanxiang.App

let private withDir f =
    let d = Path.Combine(Path.GetTempPath(), "wx-ct-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    try f d finally Directory.Delete(d, true)

[<Fact>]
let ``写入后读回同一令牌，格式与旧实现相同`` () =
    withDir (fun d ->
        let f = ClientTomlFile(Path.Combine(d, "client.toml"))
        Assert.Equal(Ok(), f.Write("127.0.0.1:8765", "tok"))
        Assert.Equal("[client]\nurl = \"ws://127.0.0.1:8765/ws\"\ntoken = \"tok\"\n", File.ReadAllText f.Path)
        Assert.Equal(Some "tok", f.TryReadToken())
        Assert.Empty(Directory.GetFiles(d, ".client.toml.tmp.*")))

[<Fact>]
let ``只有本人可读写`` () =
    if not (OperatingSystem.IsWindows()) then
        withDir (fun d ->
            let f = ClientTomlFile(Path.Combine(d, "client.toml"))
            f.Write("h", "t") |> ignore
            Assert.Equal(UnixFileMode.UserRead ||| UnixFileMode.UserWrite, File.GetUnixFileMode f.Path))

[<Fact>]
let ``覆盖已有文件`` () =
    withDir (fun d ->
        let f = ClientTomlFile(Path.Combine(d, "client.toml"))
        f.Write("h", "old") |> ignore
        f.Write("h", "new") |> ignore
        Assert.Equal(Some "new", f.TryReadToken()))

[<Fact>]
let ``不存在、空值、没有 token 行都读成 None`` () =
    withDir (fun d ->
        let p = Path.Combine(d, "client.toml")
        let f = ClientTomlFile(p)
        Assert.Equal(None, f.TryReadToken())
        File.WriteAllText(p, "token = \"\"\n")
        Assert.Equal(None, f.TryReadToken())
        File.WriteAllText(p, "[client]\nurl = \"x\"\n")
        Assert.Equal(None, f.TryReadToken()))

[<Fact>]
let ``取第一条 token 行，容忍空白与无引号`` () =
    withDir (fun d ->
        let p = Path.Combine(d, "client.toml")
        File.WriteAllText(p, "  token=abc  \ntoken = \"second\"\n")
        Assert.Equal(Some "abc", ClientTomlFile(p).TryReadToken()))

[<Fact>]
let ``目录不存在时写入返回错误而不抛`` () =
    withDir (fun d ->
        let f = ClientTomlFile(Path.Combine(d, "missing", "client.toml"))
        match f.Write("h", "t") with
        | Error _ -> ()
        | Ok() -> failwith "expected error")

[<Fact>]
let ``路径被目录占用时读成 None`` () =
    withDir (fun d ->
        let p = Path.Combine(d, "client.toml")
        Directory.CreateDirectory p |> ignore
        Assert.Equal(None, ClientTomlFile(p).TryReadToken()))
