namespace Wanxiang2.SettingsScreen

open System
open Avalonia.Controls
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ToastStrip
open Wanxiang2.OverlayDesk
open Wanxiang2.DevicesPane
open Wanxiang2.WorkspaceState

/// 命令类操作登记：
/// - WireLanSource: 测试注入局域网地址来源
/// - ApplyLoadTimeout: 等待超时（定时器与测试共用）通知
/// - ApplyDisconnected: 断开连接时通知
/// 设置 → 设备的协作（旧 Views/SettingsDevices.fs 的状态部分）：设备列表的有界等待、配对码倒计时、
/// 地址与二维码推断、重命名空名提示。界面归 DevicesPane；读取、邀请、改名、吊销以事件交给宿主。
/// 邀请码只存在于内存与界面里：不入日志、不写文件、不上传。
type DevicesDesk(_theme: ThemeManager, overlay: OverlayDesk) =
    let pane = DevicesPane()
    let requested = Event<unit>()
    let inviteRequested = Event<unit>()
    let renameRequested = Event<string * string>()
    let revokeRequested = Event<DeviceInfo>()

    let mutable devices: DeviceInfo list = []
    let mutable canInvite = false
    let mutable invite: PairingInviteView option = None
    let mutable inviteRejected: string option = None
    /// 旧主机忽略未知事件时不会回复，不能一直显示“正在读取”
    let loadWait = BoundedWait(BoundedWaits.devicesLimit)
    let mutable loadNote: string option = None
    let mutable wsUrl = ""
    let mutable lan: unit -> string list = DevicesModel.lanAddresses
    let mutable now = DateTimeOffset.UtcNow
    let mutable editing: string option = None

    let remaining () =
        match invite with
        | Some i -> i.expiresAtUtc - now
        | None -> TimeSpan.Zero

    let withQr (url: string) = url, DevicesModel.qrMatrix url

    let inviteState () =
        match canInvite, invite with
        | false, _ ->
            DevicesPaneInvite.Unavailable(
                if String.IsNullOrWhiteSpace wsUrl then "尚未连接主机。"
                else "只有这台主机上的桌面应用可以添加设备。请在桌面应用里打开「设置 → 设备 → 添加设备」。")
        | true, None ->
            DevicesPaneInvite.Offer(
                match inviteRejected with
                | Some "not-desktop" -> Some "主机没有批准这次邀请：只有主机上的桌面应用可以添加设备。"
                | Some _ -> Some "主机没有批准这次邀请，请稍后再试。"
                | None -> None)
        | true, Some i ->
            let left = remaining ()
            let expired = left <= TimeSpan.Zero
            let address =
                match DevicesModel.inviteTarget wsUrl (lan ()) with
                | InviteTarget.Reachable url -> DevicesPaneAddress.Reachable(withQr url)
                | InviteTarget.LoopbackOnly(first :: others) -> DevicesPaneAddress.LoopbackOnly(Some(withQr first), others)
                | InviteTarget.LoopbackOnly [] -> DevicesPaneAddress.LoopbackOnly(None, [])
                | InviteTarget.NoAddress -> DevicesPaneAddress.NoAddress
            DevicesPaneInvite.Code(
                DevicesModel.groupCode i.code,
                String.Join(" ", i.code.ToCharArray()),
                (if expired then "已过期" else sprintf "%s 后失效" (DevicesModel.countdown left)),
                expired,
                address)

    let row (device: DeviceInfo) : DevicesPaneRow =
        let badges =
            [ if device.current then "这台设备"
              if device.desktop then "桌面"
              if device.revoked then "已吊销" ]
        { Id = device.id
          Name = device.name
          // 保留名 wanxiang-desktop 是协议标识，给人看的是「本机桌面客户端」
          Title =
            (if String.IsNullOrWhiteSpace device.name then "未命名设备"
             elif device.desktop && device.name = "wanxiang-desktop" then "本机桌面客户端"
             else device.name)
          Meta = String.Join(" · ", badges @ [ DevicesModel.lastSeenText now device; DevicesModel.createdText device ])
          Revoked = device.revoked
          CanRename = not device.revoked && not device.desktop
          CanRevoke = not device.revoked && not device.current && not device.desktop }

    let listState () =
        match loadNote with
        | Some note -> DevicesPaneList.Note note
        | None when loadWait.IsWaiting && devices.IsEmpty -> DevicesPaneList.Note "正在读取设备列表…"
        | None when devices.IsEmpty -> DevicesPaneList.Note "还没有设备。"
        | None ->
            // 有效设备在前，已吊销在后
            devices
            |> List.sortBy (fun d -> (if d.revoked then 1 else 0), (if d.current then 0 else 1))
            |> List.map row
            |> DevicesPaneList.Rows

    let render () = pane.Render { Invite = inviteState (); List = listState (); Editing = editing }

    let timeOut () =
        if loadWait.TimeOut() then
            loadNote <- Some BoundedWaits.devicesTimeoutText
            render ()

    let beginLoad () =
        loadWait.Begin DateTimeOffset.UtcNow
        loadNote <- None

    let refresh () =
        beginLoad ()
        render ()
        requested.Trigger()

    do
        pane.AddClicked.Add(fun () -> inviteRejected <- None; inviteRequested.Trigger())
        pane.RegenerateClicked.Add(fun () -> inviteRequested.Trigger())
        pane.CloseInviteClicked.Add(fun () -> invite <- None; render ())
        pane.RefreshClicked.Add(fun () -> refresh ())
        pane.RenameClicked.Add(fun id -> editing <- Some id; render ())
        pane.RenameCancelled.Add(fun _ -> editing <- None; render ())
        pane.RenameSaved.Add(fun (id, name) ->
            if name = "" then overlay.Toast("设备名称不能为空。", ToastTone.Failure)
            else
                editing <- None
                renameRequested.Trigger((id, name))
                render ())
        pane.RevokeClicked.Add(fun id -> devices |> List.tryFind (fun d -> d.id = id) |> Option.iter revokeRequested.Trigger)
        render ()

    member _.View = pane.View
    member _.Pane = pane

    /// 单入口幂等渲染：由 SettingsDevicesVS 投影驱动
    member this.Render(vs: SettingsDevicesVS) =
        wsUrl <- vs.ServerUrl
        devices <- vs.Devices
        canInvite <- vs.CanInvite
        invite <- vs.Invite
        inviteRejected <- vs.InviteRejected
        loadNote <- vs.LoadNote
        now <- vs.Now
        if not vs.IsLoading then
            loadWait.Settle() |> ignore
        render ()

    /// 切进设备分区时：读取最新列表（开始有界等待）
    member _.Refresh() = refresh ()

    /// 等待超时（定时器与测试共用）：仍在读取则结束等待并提示主机可能较旧
    member _.ApplyLoadTimeout() = timeOut ()

    /// 测试注入局域网地址来源
    member _.WireLanSource(source: unit -> string list) =
        lan <- source
        render ()

    /// 外部推进时钟（测试与定时器共用）
    member _.Tick(nowUtc: DateTimeOffset) =
        now <- nowUtc
        if loadWait.Tick nowUtc then
            timeOut ()
        else
            render ()

    /// 读取设备列表期间连接断开：旧版主机不认识设备事件时会直接断开
    member _.ApplyDisconnected() =
        if loadWait.Settle() then
            loadNote <- Some "读取设备列表时连接断开了。如果主机版本较旧，可能还不支持设备管理，请先升级主机。"
        invite <- None
        render ()

    member _.CurrentInvite = invite
    member _.Devices = devices
    member _.CanInvite = canInvite
    member _.IsLoading = loadWait.IsWaiting
    member _.IsShown = pane.View.IsEffectivelyVisible

    /// 请求设备列表快照
    member _.ListRequested = requested.Publish
    /// 请求配对邀请（仅桌面本机客户端会被主机接受）
    member _.InviteRequested = inviteRequested.Publish
    /// (deviceId, 新名称)，已去首尾空白且非空
    member _.RenameRequested = renameRequested.Publish
    /// 吊销某台设备（确认由宿主负责，与旧代码一致）
    member _.RevokeRequested = revokeRequested.Publish
