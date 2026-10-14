module Wanxiang2.DevicesPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.DevicesPane
open Wanxiang2.DevicesPane.Tests.Probe

let private look () =
    let body = Cell 14.0
    let gap = Cell 12.0
    body, gap,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = body; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = gap; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      CodeTracking = Cell 6.0; QrModuleSize = Cell 6.0 }

let private qr = Array.init 21 (fun r -> Array.init 21 (fun c -> (r + c) % 3 = 0))

let private row id name : DevicesPaneRow =
    { Id = id; Name = name; Title = name; Meta = "创建于 10月1日"; Revoked = false; CanRename = true; CanRevoke = true }

let private pane () =
    let _, _, l = look ()
    let p = DevicesPane l
    let w = host p.View
    p, w

let private offer = { Invite = DevicesPaneInvite.Offer None; List = DevicesPaneList.Rows []; Editing = None }

[<Fact>]
let ``device list marks current desktop online and revoked and sorts revoked last`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let rows =
            [ { row "dev-desktop" "wanxiang-desktop" with Title = "本机桌面客户端"; Meta = "这台设备 · 桌面 · 在线"; CanRename = false; CanRevoke = false }
              { row "dev-phone" "手机" with Meta = "在线" }
              { row "dev-revoked" "旧手机" with Meta = "已吊销"; Revoked = true } ]
        p.Render { offer with List = DevicesPaneList.Rows rows }
        let list = (byId p.View "devices-list").Value
        let ids =
            descendants list
            |> Seq.map AutomationProperties.GetAutomationId
            |> Seq.filter (fun id -> not (isNull id) && id.StartsWith "devices-row-")
            |> List.ofSeq
        Assert.Equal<string list>([ "devices-row-dev-desktop"; "devices-row-dev-phone"; "devices-row-dev-revoked" ], ids)
        let text = textOf list
        Assert.Contains("本机桌面客户端", text)
        Assert.Contains("已吊销", text)
        Assert.True((byId p.View "devices-revoke-dev-desktop").IsNone)
        Assert.True((byId p.View "devices-rename-dev-desktop").IsNone)
        Assert.True((byId p.View "devices-revoke-dev-phone").IsSome)
        Assert.True((byId p.View "devices-revoke-dev-revoked").IsNone)
        Assert.True((byId p.View "devices-rename-dev-revoked").IsNone))

[<Fact>]
let ``revoke button passes the device to the action`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let seen = ResizeArray<string>()
        p.RevokeClicked.Add seen.Add
        p.Render { offer with List = DevicesPaneList.Rows [ row "dev-phone" "手机" ] }
        click (byId p.View "devices-revoke-dev-phone").Value
        Assert.Equal<string list>([ "dev-phone" ], List.ofSeq seen))

[<Fact>]
let ``rename edits inline trims and sends the new name`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let starts, saves, cancels = ResizeArray<string>(), ResizeArray<string * string>(), ResizeArray<string>()
        p.RenameClicked.Add starts.Add
        p.RenameSaved.Add saves.Add
        p.RenameCancelled.Add cancels.Add
        let state = { offer with List = DevicesPaneList.Rows [ row "dev-phone" "手机" ] }
        p.Render state
        click (byId p.View "devices-rename-dev-phone").Value
        Assert.Equal<string list>([ "dev-phone" ], List.ofSeq starts)
        p.Render { state with Editing = Some "dev-phone" }
        let box = (byId p.View "devices-name-dev-phone").Value :?> TextBox
        Assert.Equal("手机", box.Text)
        Assert.Same(box, p.NameBox.Value)
        Dispatcher.UIThread.RunJobs()
        Assert.True box.IsFocused
        box.Text <- "  书房平板 "
        click (byId p.View "devices-name-save-dev-phone").Value
        Assert.Equal<(string * string) list>([ "dev-phone", "书房平板" ], List.ofSeq saves)
        box.Text <- "   "
        click (byId p.View "devices-name-save-dev-phone").Value
        Assert.Equal(("dev-phone", ""), saves[1])
        click (byId p.View "devices-name-cancel-dev-phone").Value
        Assert.Equal<string list>([ "dev-phone" ], List.ofSeq cancels)
        p.Render state
        Assert.True((byId p.View "devices-name-dev-phone").IsNone))

[<Fact>]
let ``only a desktop capable connection sees the add device button`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let adds = ResizeArray<unit>()
        p.AddClicked.Add adds.Add
        p.Render { offer with Invite = DevicesPaneInvite.Unavailable "只有这台主机上的桌面应用可以添加设备。" }
        Assert.True((byId p.View "devices-add").IsNone)
        Assert.True((byId p.View "devices-invite-unavailable").IsSome)
        p.Render offer
        click (byId p.View "devices-add").Value
        Assert.Equal(1, adds.Count))

[<Fact>]
let ``invite card shows grouped code address qr and countdown`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let again, closed = ResizeArray<unit>(), ResizeArray<unit>()
        p.RegenerateClicked.Add again.Add
        p.CloseInviteClicked.Add closed.Add
        let code countdown expired =
            { offer with Invite = DevicesPaneInvite.Code("012 345", "0 1 2 3 4 5", countdown, expired, DevicesPaneAddress.Reachable("http://192.168.1.5:8765/", qr)) }
        p.Render(code "5:00 后失效" false)
        Assert.Equal("012 345", textOf (byId p.View "devices-code").Value)
        Assert.Equal("配对码 0 1 2 3 4 5", AutomationProperties.GetName (byId p.View "devices-code").Value)
        Assert.Equal("5:00 后失效", textOf (byId p.View "devices-countdown").Value)
        Assert.Equal("http://192.168.1.5:8765/", ((byId p.View "devices-address").Value :?> TextBlock).Text)
        Assert.Equal("http://192.168.1.5:8765/", ((byId p.View "devices-qr").Value :?> QrPlate).Text)
        p.Render(code "已过期" true)
        Assert.Equal("已过期", textOf (byId p.View "devices-countdown").Value)
        Assert.Contains("button-primary", (byId p.View "devices-regenerate").Value.Classes)
        click (byId p.View "devices-regenerate").Value
        click (byId p.View "devices-close-invite").Value
        Assert.Equal(1, again.Count)
        Assert.Equal(1, closed.Count))

[<Fact>]
let ``loopback only host explains the listen setting and uses a lan address for the qr`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        let addr = DevicesPaneAddress.LoopbackOnly(Some("http://192.168.1.9:8765/", qr), [ "http://10.0.0.4:8765/" ])
        p.Render { offer with Invite = DevicesPaneInvite.Code("111 111", "1 1 1 1 1 1", "5:00 后失效", false, addr) }
        let text = textOf p.View
        Assert.Contains("listen", text)
        Assert.Contains("其他网卡地址：http://10.0.0.4:8765/", text)
        Assert.Equal("http://192.168.1.9:8765/", ((byId p.View "devices-qr").Value :?> QrPlate).Text)
        p.Render { offer with Invite = DevicesPaneInvite.Code("222 222", "2 2 2 2 2 2", "5:00 后失效", false, DevicesPaneAddress.LoopbackOnly(None, [])) }
        Assert.True((byId p.View "devices-qr").IsNone)
        Assert.Contains("listen", textOf p.View))

[<Fact>]
let ``rejected invite explains that only the desktop app can add devices`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        p.Render { offer with Invite = DevicesPaneInvite.Offer(Some "主机没有批准这次邀请：只有主机上的桌面应用可以添加设备。") }
        Assert.Contains("桌面应用", textOf p.View)
        Assert.True((byId p.View "devices-code").IsNone))

[<Fact>]
let ``qr view measures with a quiet zone and draws on a white background`` () =
    Headless.run (fun () ->
        let size = Cell 4.0
        let view = QrPlate("http://h/", qr, size)
        view.Measure(Size(1000.0, 1000.0))
        Assert.Equal(float (21 + 8) * 4.0, view.DesiredSize.Width)
        size.Set 5.0
        view.Measure(Size(1000.0, 1000.0))
        Assert.Equal(float (21 + 8) * 5.0, view.DesiredSize.Height))

[<Fact>]
let ``look values stay live after the pane is shown`` () =
    Headless.run (fun () ->
        let body, gap, l = look ()
        let p = DevicesPane l
        let _ = host p.View
        p.Render { offer with List = DevicesPaneList.Rows [ row "dev-phone" "手机" ] }
        let list = (byId p.View "devices-list").Value :?> StackPanel
        gap.Set 20.0
        body.Set 18.0
        Assert.Equal(20.0, list.Spacing)
        let title = descendants list |> Seq.pick (fun c -> match c with :? TextBlock as t when t.Text = "手机" -> Some t | _ -> None)
        Assert.Equal(18.0, title.FontSize))

[<Fact>]
let ``empty list note replaces the rows`` () =
    Headless.run (fun () ->
        let p, _ = pane ()
        p.Render { offer with List = DevicesPaneList.Note "正在读取设备列表…" }
        Assert.Equal("正在读取设备列表…", textOf (byId p.View "devices-list").Value))
