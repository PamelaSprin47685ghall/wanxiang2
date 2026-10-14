module Tests

open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

/// 金样由旧代码（从旧视图文件原样摘出的函数）在同一组输入上生成
let render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let js (s: string) = Text.Json.JsonSerializer.Serialize s
    line (sprintf "inviteLifetime %O" DevicesModel.inviteLifetime)
    for s in Samples.spans do line (sprintf "countdown %g -> %s" s (DevicesModel.countdown (TimeSpan.FromSeconds s)))
    for c in Samples.codes do line (sprintf "groupCode %s -> %s" (js c) (js (DevicesModel.groupCode c)))
    line (sprintf "groupCode null -> %s" (js (DevicesModel.groupCode null)))
    for u in Samples.urls do
        line (sprintf "httpOrigin %s -> %A" (js u) (DevicesModel.httpOrigin u))
        line (sprintf "isLoopbackHost %s -> %b" (js u) (DevicesModel.isLoopbackHost u))
        line (sprintf "inviteTarget %s -> %A" (js u) (DevicesModel.inviteTarget u [ "192.168.1.9"; "10.0.0.2" ]))
        line (sprintf "inviteTarget-nolan %s -> %A" (js u) (DevicesModel.inviteTarget u []))
    let devices = DevicesModel.parse (Text.Json.Nodes.JsonNode.Parse(Samples.deviceJson).AsArray())
    for d in devices do
        line (sprintf "device %A" d)
        line (sprintf "  lastSeenText %s createdText %s" (DevicesModel.lastSeenText Samples.now d) (DevicesModel.createdText d))
    for t in [ "https://example.com/"; "" ; "WX-123456" ] do
        let m = DevicesModel.qrMatrix t
        line (sprintf "qr %s %d %s" (js t) m.Length (String.Join("|", m |> Array.map (fun r -> String(r |> Array.map (fun b -> if b then '#' else '.'))))))
    sb.ToString()

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "devices.txt"))

/// 日期文案用本地时区；金样在 UTC 下生成。非 UTC 机器上只比对不含日期的行
[<Fact>]
let ``golden: device texts, invite targets and qr matrices match the old DevicesModel`` () =
    let utc = TimeZoneInfo.Local.GetUtcOffset(Samples.now) = TimeSpan.Zero
    let keep (s: string) = utc || not (s.Contains "lastSeenText")
    let lines (s: string) = s.Split('\n') |> Array.filter keep
    Assert.Equal<string[]>(lines (expected ()), lines (render ()))


[<Fact>]
let ``countdown rounds up and never goes negative`` () =
    Assert.Equal("0:00", DevicesModel.countdown (TimeSpan.FromSeconds -3.0))
    Assert.Equal("0:01", DevicesModel.countdown (TimeSpan.FromMilliseconds 1.0))
    Assert.Equal("5:00", DevicesModel.countdown DevicesModel.inviteLifetime)

[<Fact>]
let ``only six digit codes are grouped`` () =
    Assert.Equal("123 456", DevicesModel.groupCode "123456")
    Assert.Equal("1234567", DevicesModel.groupCode "1234567")

[<Fact>]
let ``loopback host offers lan candidates on the same port`` () =
    match DevicesModel.inviteTarget "ws://127.0.0.1:8787/ws" [ "192.168.0.7" ] with
    | InviteTarget.LoopbackOnly [ c ] -> Assert.Equal("http://192.168.0.7:8787/", c)
    | other -> failwithf "%A" other

[<Fact>]
let ``non websocket urls have no invite address`` () =
    Assert.Equal(InviteTarget.NoAddress, DevicesModel.inviteTarget "https://example.com/" [ "10.0.0.1" ])
    Assert.Equal(None, DevicesModel.httpOrigin null)
    Assert.False(DevicesModel.isLoopbackHost null)

[<Fact>]
let ``malformed device entries are skipped or defaulted`` () =
    let parsed = DevicesModel.parse (Text.Json.Nodes.JsonNode.Parse("""[1, {"id":"x","revoked":"yes"}]""").AsArray())
    match parsed with
    | [ d ] ->
        Assert.Equal("x", d.id)
        Assert.False d.revoked
        Assert.Equal(None, d.lastSeen)
    | other -> failwithf "%A" other

[<Fact>]
let ``revoked wins over online in the last seen text`` () =
    let d = { id = "a"; name = ""; createdAt = None; lastSeen = None; revoked = true; online = true; current = false; desktop = false }
    Assert.Equal("已吊销", DevicesModel.lastSeenText DateTimeOffset.UtcNow d)
    Assert.Equal("在线", DevicesModel.lastSeenText DateTimeOffset.UtcNow { d with revoked = false })
    Assert.Equal("从未连接", DevicesModel.lastSeenText DateTimeOffset.UtcNow { d with revoked = false; online = false })

[<Fact>]
let ``qr matrix is square and non-empty`` () =
    let m = DevicesModel.qrMatrix "https://example.com/"
    Assert.True(m.Length > 0)
    Assert.All(m, fun row -> Assert.Equal(m.Length, row.Length))
