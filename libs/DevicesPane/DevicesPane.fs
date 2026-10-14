namespace Wanxiang2.DevicesPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 二维码：白底黑模块、四周 4 模块静区，与主题无关（扫码器要对比度）。矩阵由组合者给（DevicesModel.qrMatrix）。
type QrPlate(text: string, matrix: bool[][], moduleSize: IObservable<float>) as this =
    inherit Control()
    let quiet = 4
    let modules = matrix.Length + quiet * 2
    let mutable size = 0.0
    do
        moduleSize.Subscribe(fun v ->
            size <- v
            this.InvalidateMeasure()
            this.InvalidateVisual())
        |> ignore

    member _.Text = text
    member _.ModuleCount = matrix.Length
    member _.IsDark(row: int, col: int) = matrix[row][col]

    override _.MeasureOverride(_) =
        let side = float modules * size
        Size(side, side)

    override _.Render(context) =
        let side = float modules * size
        context.FillRectangle(Brushes.White, Rect(0.0, 0.0, side, side))
        for r in 0 .. matrix.Length - 1 do
            for c in 0 .. matrix.Length - 1 do
                if matrix[r][c] then
                    context.FillRectangle(Brushes.Black, Rect(float (c + quiet) * size, float (r + quiet) * size, size, size))

/// 新设备该打开的地址（组合者从 DevicesModel.inviteTarget 得出；带二维码矩阵）
[<RequireQualifiedAccess>]
type DevicesPaneAddress =
    /// 直接可达
    | Reachable of url: string * qr: bool[][]
    /// 主机只监听本机：首个局域网地址（可无）与其余网卡地址
    | LoopbackOnly of first: (string * bool[][]) option * others: string list
    | NoAddress

/// 邀请卡的一帧
[<RequireQualifiedAccess>]
type DevicesPaneInvite =
    /// 这条连接不能添加设备：说明文字
    | Unavailable of text: string
    /// 可添加；上次被拒的说明（可无）
    | Offer of rejected: string option
    /// 已有配对码：分组后的码、逐字读法、倒计时文字、是否过期、地址
    | Code of grouped: string * spoken: string * countdown: string * expired: bool * address: DevicesPaneAddress

/// 一台设备
type DevicesPaneRow =
    { Id: string
      /// 原名（读屏名与编辑框初值）
      Name: string
      /// 给人看的名字（未命名、本机桌面客户端已换好）
      Title: string
      /// 「这台设备 · 桌面 · 在线 · 创建于…」
      Meta: string
      Revoked: bool
      CanRename: bool
      CanRevoke: bool }

/// 列表区的一帧
[<RequireQualifiedAccess>]
type DevicesPaneList =
    /// 一行说明（读取中、超时、断开、还没有设备）
    | Note of string
    /// 已排好序的行
    | Rows of DevicesPaneRow list

type DevicesPaneState =
    { Invite: DevicesPaneInvite
      List: DevicesPaneList
      /// 正在重命名的设备
      Editing: string option }

/// 设置 → 设备分区（迁自旧 Views/SettingsDevices.fs 的界面部分）：标题与说明、添加设备 / 配对码卡（码、倒计时、地址、二维码）、
/// 已授权设备列表（重命名、吊销、行内编辑）、刷新键。读取、有界等待、倒计时时钟、地址推断由组合者接。
type DevicesPane(look: DevicesPaneLook) =
    let kit = Kit(look)
    let add = Event<unit>()
    let regenerate = Event<unit>()
    let closeInvite = Event<unit>()
    let refresh = Event<unit>()
    let renameStart = Event<string>()
    let renameSave = Event<string * string>()
    let renameCancel = Event<string>()
    let revoke = Event<string>()

    let root = StackPanel(Orientation = Orientation.Vertical)
    let inviteHost = ContentControl()
    let listHost = StackPanel(Orientation = Orientation.Vertical)
    let mutable nameBox: TextBox option = None

    let tag (control: Control) (label: string) (id: string) =
        AutomationProperties.SetName(control, label)
        AutomationProperties.SetAutomationId(control, id)

    let spacer () = Border(Height = 0.0) :> Control

    let addressBlock (url: string) (qr: bool[][]) : Control list =
        let address = SelectableTextBlock(Text = url, TextWrapping = TextWrapping.Wrap)
        kit.Live(look.MonoFont, fun f -> address.FontFamily <- f)
        kit.Live(look.SmallFontSize, fun v -> address.FontSize <- v)
        tag address url "devices-address"
        let plate = QrPlate(url, qr, look.QrModuleSize)
        plate.HorizontalAlignment <- HorizontalAlignment.Left
        tag plate (sprintf "二维码，扫描后打开 %s" url) "devices-qr"
        [ address :> Control; plate :> Control ]

    let inviteCard (invite: DevicesPaneInvite) : Control =
        let card (children: Control list) =
            kit.Card(look.Space2xl, look.Space2xl, look.RadiusLg, kit.VStack(look.SpaceMd, children)) :> Control
        match invite with
        | DevicesPaneInvite.Unavailable text ->
            let caption = kit.Caption text
            tag caption text "devices-invite-unavailable"
            card [ caption ]
        | DevicesPaneInvite.Offer rejected ->
            let button = kit.Button(Tone.Primary, "添加设备", add.Trigger)
            button.HorizontalAlignment <- HorizontalAlignment.Left
            tag button "添加设备" "devices-add"
            card
                [ kit.Label "把手机、平板或另一台电脑接入这台主机。"
                  kit.Caption "会生成一个 6 位配对码和地址，5 分钟内有效，只能用一次。"
                  (match rejected with
                   | Some r ->
                       let t = kit.Caption r
                       t.Foreground <- look.Danger
                       t :> Control
                   | None -> spacer ())
                  button ]
        | DevicesPaneInvite.Code(grouped, spoken, countdown, expired, address) ->
            let codeText =
                TextBlock(
                    Text = grouped,
                    FontWeight = FontWeight.Medium,
                    Foreground = (if expired then look.Muted else look.Text),
                    HorizontalAlignment = HorizontalAlignment.Left)
            kit.Live(look.DisplayFontSize, fun v -> codeText.FontSize <- v)
            kit.Live(look.MonoFont, fun f -> codeText.FontFamily <- f)
            kit.Live(look.CodeTracking, fun v -> codeText.LetterSpacing <- v)
            tag codeText (sprintf "配对码 %s" spoken) "devices-code"
            let countdownText = kit.Text(countdown, look.SmallFontSize, (if expired then look.Danger else look.Muted))
            tag countdownText countdown "devices-countdown"
            let addressControls: Control list =
                match address with
                | DevicesPaneAddress.Reachable(url, qr) ->
                    [ kit.Caption "在新设备上打开这个地址（或扫码），再输入上面的配对码：" :> Control ] @ addressBlock url qr
                | DevicesPaneAddress.LoopbackOnly(first, others) ->
                    let hint =
                        kit.Caption
                            "这台主机只监听本机地址，手机暂时打不开它。要让手机接入，请把 config.toml 里的 listen 改成 0.0.0.0:端口（局域网内使用建议配置 TLS），或放在反向代理后面。"
                    match first with
                    | Some(url, qr) ->
                        [ hint :> Control; kit.Caption "改好后，在新设备上打开这个局域网地址（或扫码）：" :> Control ]
                        @ addressBlock url qr
                        @ (if others.IsEmpty then [] else [ kit.Caption(sprintf "其他网卡地址：%s" (String.Join("、", others))) :> Control ])
                    | None -> [ hint :> Control ]
                | DevicesPaneAddress.NoAddress -> [ kit.Caption "没有可用的主机地址。" :> Control ]
            let again = kit.Button((if expired then Tone.Primary else Tone.Secondary), "重新生成", regenerate.Trigger)
            tag again "重新生成配对码" "devices-regenerate"
            let close = kit.Button(Tone.Ghost, "完成", closeInvite.Trigger)
            tag close "关闭配对码" "devices-close-invite"
            card (
                [ kit.Caption "配对码" :> Control; codeText :> Control; countdownText :> Control ]
                @ addressControls
                @ [ kit.HStack(look.SpaceMd, [ again :> Control; close :> Control ]) :> Control ])

    let deviceRow (editing: string option) (device: DevicesPaneRow) : Control =
        let title = kit.Text(device.Title, look.BodyFontSize, (if device.Revoked then look.Muted else look.Text))
        title.FontWeight <- FontWeight.Medium
        title.TextWrapping <- TextWrapping.Wrap
        let meta = kit.Caption device.Meta
        let buttons: Control list =
            if device.Revoked then []
            else
                [ if device.CanRename then
                      let rename = kit.Button(Tone.Ghost, "重命名", (fun () -> renameStart.Trigger device.Id))
                      tag rename (sprintf "重命名设备 %s" device.Name) (sprintf "devices-rename-%s" device.Id)
                      yield rename :> Control
                  if device.CanRevoke then
                      let r = kit.Button(Tone.Danger, "吊销", (fun () -> revoke.Trigger device.Id))
                      tag r (sprintf "吊销设备 %s" device.Name) (sprintf "devices-revoke-%s" device.Id)
                      yield r :> Control ]
        let editor () : Control =
            let shell, box = kit.TextField "设备名称"
            box.Text <- device.Name
            box.MaxLength <- 64
            tag box "设备名称" (sprintf "devices-name-%s" device.Id)
            nameBox <- Some box
            let save () =
                let value = if isNull box.Text then "" else box.Text.Trim()
                renameSave.Trigger((device.Id, value))
            box.KeyDown.Add(fun e ->
                if e.Key = Key.Enter then
                    e.Handled <- true
                    save ())
            let saveButton = kit.Button(Tone.Primary, "保存", save)
            tag saveButton "保存设备名称" (sprintf "devices-name-save-%s" device.Id)
            let cancel = kit.Button(Tone.Ghost, "取消", (fun () -> renameCancel.Trigger device.Id))
            tag cancel "取消重命名" (sprintf "devices-name-cancel-%s" device.Id)
            Dispatcher.UIThread.Post(fun () -> box.Focus() |> ignore)
            kit.VStack(look.SpaceMd, [ shell :> Control; kit.HStack(look.SpaceMd, [ saveButton :> Control; cancel :> Control ]) :> Control ])
            :> Control
        let content: Control list =
            if editing = Some device.Id && not device.Revoked then [ editor () ]
            else [ title; meta; kit.HStack(look.SpaceMd, buttons) ]
        let card = kit.Card(look.SpaceXl, look.SpaceMd, look.RadiusMd, kit.VStack(look.SpaceXs, content))
        tag card (sprintf "设备 %s" device.Name) (sprintf "devices-row-%s" device.Id)
        card :> Control

    do
        kit.Live(look.Space4xl, fun v -> root.Spacing <- v)
        kit.Live(look.SpaceMd, fun v -> listHost.Spacing <- v)
        let refreshButton = kit.Button(Tone.Ghost, "刷新", refresh.Trigger)
        tag refreshButton "刷新设备列表" "devices-refresh"
        refreshButton.HorizontalAlignment <- HorizontalAlignment.Left
        tag listHost "设备列表" "devices-list"
        root.Children.Add(
            kit.VStack(
                look.SpaceXs,
                [ kit.Heading "设备" :> Control
                  kit.Caption "连接到这台主机的手机、浏览器和桌面应用。吊销后该设备立即断开，需要重新配对才能再连。" :> Control ]))
        root.Children.Add inviteHost
        root.Children.Add(kit.VStack(look.SpaceMd, [ kit.SectionLabel "已授权的设备" :> Control; listHost :> Control; refreshButton :> Control ]))

    member _.View = root :> Control

    /// 同步一帧（整块重建，与旧实现相同）
    member _.Render(state: DevicesPaneState) =
        nameBox <- None
        inviteHost.Content <- inviteCard state.Invite
        listHost.Children.Clear()
        match state.List with
        | DevicesPaneList.Note text -> listHost.Children.Add(kit.Caption text)
        | DevicesPaneList.Rows rows -> for r in rows do listHost.Children.Add(deviceRow state.Editing r)

    /// 正在编辑时的名称输入框（组合者给空名提示后可把焦点送回）
    member _.NameBox = nameBox

    member _.AddClicked = add.Publish
    member _.RegenerateClicked = regenerate.Publish
    member _.CloseInviteClicked = closeInvite.Publish
    member _.RefreshClicked = refresh.Publish
    /// 设备 id
    member _.RenameClicked = renameStart.Publish
    /// 设备 id 与去掉首尾空白的新名（可能为空，由组合者提示）
    member _.RenameSaved = renameSave.Publish
    member _.RenameCancelled = renameCancel.Publish
    member _.RevokeClicked = revoke.Publish
