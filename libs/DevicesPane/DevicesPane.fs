namespace Wanxiang2.DevicesPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState
open Avalonia.Threading

/// 二维码：白底黑模块、四周 4 模块静区，与主题无关（扫码器要对比度）。矩阵由组合者给（DevicesModel.qrMatrix）。
type QrPlate(text: string, matrix: bool[][], ?moduleSize: float) =
    inherit Control()
    let quiet = 4
    let modules = matrix.Length + quiet * 2
    let mutable size = 0.0
    do size <- defaultArg moduleSize 6.0

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
                    context.FillRectangle(
                        Brushes.Black,
                        Rect(float (c + quiet) * size, float (r + quiet) * size, size, size)
                    )

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
    {
        Id: string
        /// 原名（读屏名与编辑框初值）
        Name: string
        /// 给人看的名字（未命名、本机桌面客户端已换好）
        Title: string
        /// 「这台设备 · 桌面 · 在线 · 创建于…」
        Meta: string
        Revoked: bool
        CanRename: bool
        CanRevoke: bool
    }

/// 列表区的一帧
[<RequireQualifiedAccess>]
type DevicesPaneList =
    /// 一行说明（读取中、超时、断开、还没有设备）
    | Note of string
    /// 已排好序的行
    | Rows of DevicesPaneRow list

type DevicesPaneState =
    {
        Invite: DevicesPaneInvite
        List: DevicesPaneList
        /// 正在重命名的设备
        Editing: string option
    }

/// 设置 → 设备分区（迁自旧 Views/SettingsDevices.fs 的界面部分）：标题与说明、添加设备 / 配对码卡（码、倒计时、地址、二维码）、
/// 已授权设备列表（重命名、吊销、行内编辑）、刷新键。读取、有界等待、倒计时时钟、地址推断由组合者接。
type DevicesPane() =
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
        address.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore
        address.FontSize <- 13.0
        address.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        tag address url "devices-address"
        let plate = QrPlate(url, qr)
        plate.HorizontalAlignment <- HorizontalAlignment.Left
        tag plate (sprintf "二维码，扫描后打开 %s" url) "devices-qr"
        [ address :> Control; plate :> Control ]

    let inviteCard (invite: DevicesPaneInvite) : Control =
        let makeCard (children: Control list) =
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)

            for c in children do
                p.Children.Add c

            Ui.card p :> Control

        match invite with
        | DevicesPaneInvite.Unavailable text ->
            let caption = Ui.caption text
            tag caption text "devices-invite-unavailable"
            makeCard [ caption ]
        | DevicesPaneInvite.Offer rejected ->
            let button = Ui.button "primary" "添加设备" add.Trigger
            button.HorizontalAlignment <- HorizontalAlignment.Left
            tag button "添加设备" "devices-add"

            makeCard
                [
                    Ui.label "把手机、平板或另一台电脑接入这台主机。"
                    Ui.caption "会生成一个 6 位配对码和地址，5 分钟内有效，只能用一次。"
                    (match rejected with
                     | Some r ->
                         let t = Ui.caption r
                         t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
                         t :> Control
                     | None -> spacer ())
                    button
                ]
        | DevicesPaneInvite.Code(grouped, spoken, countdown, expired, address) ->
            let codeText =
                TextBlock(
                    Text = grouped,
                    FontWeight = FontWeight.Medium,
                    FontSize = 28.0,
                    LetterSpacing = 6.0,
                    HorizontalAlignment = HorizontalAlignment.Left
                )

            codeText.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

            if expired then
                codeText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
                |> ignore
            else
                codeText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

            tag codeText (sprintf "配对码 %s" spoken) "devices-code"
            let countdownText = TextBlock(Text = countdown, FontSize = 13.0)

            if expired then
                countdownText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger")
                |> ignore
            else
                countdownText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
                |> ignore

            tag countdownText countdown "devices-countdown"

            let addressControls: Control list =
                match address with
                | DevicesPaneAddress.Reachable(url, qr) ->
                    [ Ui.caption "在新设备上打开这个地址（或扫码），再输入上面的配对码：" :> Control ] @ addressBlock url qr
                | DevicesPaneAddress.LoopbackOnly(first, others) ->
                    let hint =
                        Ui.caption
                            "这台主机只监听本机地址，手机暂时打不开它。要让手机接入，请把 config.toml 里的 listen 改成 0.0.0.0:端口（局域网内使用建议配置 TLS），或放在反向代理后面。"

                    match first with
                    | Some(url, qr) ->
                        [ hint :> Control; Ui.caption "改好后，在新设备上打开这个局域网地址（或扫码）：" :> Control ]
                        @ addressBlock url qr
                        @ (if others.IsEmpty then
                               []
                           else
                               [ Ui.caption (sprintf "其他网卡地址：%s" (String.Join("、", others))) :> Control ])
                    | None -> [ hint :> Control ]
                | DevicesPaneAddress.NoAddress -> [ Ui.caption "没有可用的主机地址。" :> Control ]

            let again =
                Ui.button (if expired then "primary" else "secondary") "重新生成" regenerate.Trigger

            tag again "重新生成配对码" "devices-regenerate"
            let close = Ui.button "ghost" "完成" closeInvite.Trigger
            tag close "关闭配对码" "devices-close-invite"
            let buttonRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
            buttonRow.Children.Add again
            buttonRow.Children.Add close

            makeCard (
                [ Ui.caption "配对码" :> Control; codeText :> Control; countdownText :> Control ]
                @ addressControls
                @ [ buttonRow :> Control ]
            )

    let deviceRow (editing: string option) (device: DevicesPaneRow) : Control =
        let title = TextBlock(Text = device.Title, FontSize = 14.0)

        if device.Revoked then
            title.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        else
            title.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

        title.FontWeight <- FontWeight.Medium
        title.TextWrapping <- TextWrapping.Wrap
        let meta = Ui.caption device.Meta

        let buttons: Control list =
            if device.Revoked then
                []
            else
                [
                    if device.CanRename then
                        let rename = Ui.button "ghost" "重命名" (fun () -> renameStart.Trigger device.Id)
                        tag rename (sprintf "重命名设备 %s" device.Name) (sprintf "devices-rename-%s" device.Id)
                        yield rename :> Control
                    if device.CanRevoke then
                        let r = Ui.button "danger" "吊销" (fun () -> revoke.Trigger device.Id)
                        tag r (sprintf "吊销设备 %s" device.Name) (sprintf "devices-revoke-%s" device.Id)
                        yield r :> Control
                ]

        let editor () : Control =
            let box = Ui.textField "设备名称"
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

            let saveButton = Ui.button "primary" "保存" save
            tag saveButton "保存设备名称" (sprintf "devices-name-save-%s" device.Id)
            let cancel = Ui.button "ghost" "取消" (fun () -> renameCancel.Trigger device.Id)
            tag cancel "取消重命名" (sprintf "devices-name-cancel-%s" device.Id)
            box.AttachedToVisualTree.Add(fun _ -> box.Focus() |> ignore)
            let btnRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
            btnRow.Children.Add saveButton
            btnRow.Children.Add cancel
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
            p.Children.Add box
            p.Children.Add btnRow
            p :> Control

        let buttonStack = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)

        for b in buttons do
            buttonStack.Children.Add b

        let content: Control list =
            if editing = Some device.Id && not device.Revoked then
                [ editor () ]
            else
                [ title :> Control; meta :> Control; buttonStack :> Control ]

        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)

        for c in content do
            p.Children.Add c

        let card = Ui.card p
        tag card (sprintf "设备 %s" device.Name) (sprintf "devices-row-%s" device.Id)
        card :> Control

    do
        root.Spacing <- 32.0
        listHost.Spacing <- 12.0
        let refreshButton = Ui.button "ghost" "刷新" refresh.Trigger
        tag refreshButton "刷新设备列表" "devices-refresh"
        refreshButton.HorizontalAlignment <- HorizontalAlignment.Left
        tag listHost "设备列表" "devices-list"
        let header = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        header.Children.Add(Ui.heading "设备")
        header.Children.Add(Ui.caption "连接到这台主机的手机、浏览器和桌面应用。吊销后该设备立即断开，需要重新配对才能再连。")
        root.Children.Add header
        root.Children.Add inviteHost
        let authorized = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        authorized.Children.Add(Ui.sectionLabel "已授权的设备")
        authorized.Children.Add listHost
        authorized.Children.Add refreshButton
        root.Children.Add authorized

    member _.View = root :> Control

    /// 同步一帧（整块重建，与旧实现相同）
    member _.Render(state: DevicesPaneState) =
        nameBox <- None
        inviteHost.Content <- inviteCard state.Invite
        listHost.Children.Clear()

        match state.List with
        | DevicesPaneList.Note text -> listHost.Children.Add(Ui.caption text)
        | DevicesPaneList.Rows rows ->
            for r in rows do
                listHost.Children.Add(deviceRow state.Editing r)

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
