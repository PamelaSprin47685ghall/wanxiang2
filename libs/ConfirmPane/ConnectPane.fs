namespace Wanxiang2.ConnectPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Input.TextInput
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Wanxiang2.WorkspaceState

/// 一次连接请求
type ConnectAsk =
    {
        Url: string
        Token: string option
        UsePairing: bool
    }

/// 配对码输入的规则（组合者从 PairingCodeInput / PairingHint 给）
type CodeRules =
    {
        Normalize: string -> string
        LetterSpacing: string -> float
        Hint: string -> string
    }

/// 连接对话框的内容（迁自旧 Views/Dialogs.fs 的 connectWithClosed）：地址 + 令牌，或切到配对码。
/// 失败归类、重试种类、自动提交时机归组合者；本控件只管字段、显隐、焦点与事件。
type ConnectPane(?defaultUrl: string, ?rules: CodeRules) =
    let connect = Event<ConnectAsk>()
    let codeSubmitted = Event<string>()
    let codeIncomplete = Event<unit>()
    let codeChanged = Event<string>()
    let retry = Event<unit>()
    let cancelled = Event<unit>()
    let text (b: TextBox) = if isNull b.Text then "" else b.Text

    let effectiveRules =
        defaultArg
            rules
            {
                Normalize = fun s -> s
                LetterSpacing = fun _ -> 0.0
                Hint = fun _ -> "输入配对码以连接"
            }

    let initialUrl = defaultArg defaultUrl "ws://127.0.0.1:8765/ws"

    let urlBox = TextBox(PlaceholderText = "ws://127.0.0.1:8765/ws")
    do urlBox.Classes.Add "control-stroke"
    do AutomationProperties.SetName(urlBox, "主机地址")
    let tokenBox = TextBox(PlaceholderText = "有令牌就粘贴，没有就用配对码")
    do tokenBox.Classes.Add "control-stroke"
    do AutomationProperties.SetName(tokenBox, "访问令牌")

    let fieldGroup label (box: Control) =
        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        let lbl = TextBlock(Text = label, FontWeight = FontWeight.Medium)
        lbl.Classes.Add "muted"
        p.Children.Add lbl
        p.Children.Add box
        p

    let urlField = fieldGroup "主机地址" urlBox
    let tokenField = fieldGroup "访问令牌" tokenBox
    let codeBox = TextBox(PlaceholderText = "6 位配对码")
    do codeBox.Classes.Add "control-stroke"
    do AutomationProperties.SetName(codeBox, "配对码")

    let pairingHint =
        TextBlock(Text = effectiveRules.Hint initialUrl, TextWrapping = TextWrapping.Wrap)

    do pairingHint.Classes.Add "muted"

    let codeSection =
        let inner = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
        let lbl = TextBlock(Text = "配对码", FontWeight = FontWeight.Medium)
        lbl.Classes.Add "muted"
        inner.Children.Add lbl
        inner.Children.Add codeBox
        inner.Children.Add pairingHint

        let card =
            Border(Child = inner, Padding = Thickness 16.0, CornerRadius = CornerRadius 8.0)

        card.Classes.Add "grouping-card"
        card.IsVisible <- false
        card

    let status =
        let t = TextBlock(Text = "", TextWrapping = TextWrapping.Wrap)
        t.IsVisible <- false
        AutomationProperties.SetLiveSetting(t, AutomationLiveSetting.Assertive)
        t.Classes.Add "muted"
        t

    let retryButton =
        let b = Button(Content = "重试连接")
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        b.Click.Add(fun _ -> retry.Trigger())
        b.IsVisible <- false
        b.HorizontalAlignment <- HorizontalAlignment.Left
        b

    let showMessage (message: string) (failure: bool) =
        status.Text <- message
        let has = not (String.IsNullOrWhiteSpace message)
        status.IsVisible <- has
        AutomationProperties.SetName(status, message)

        if failure && has then
            status.Classes.Remove "muted" |> ignore
            status.Classes.Add "danger"
        else
            status.Classes.Remove "danger" |> ignore
            status.Classes.Add "muted"

    let connectAction () =
        let url = (text urlBox).Trim()
        let token = (text tokenBox).Trim()

        if String.IsNullOrWhiteSpace url then
            retryButton.IsVisible <- false
            showMessage "请填写主机地址。" false
        else
            connect.Trigger
                {
                    Url = url
                    Token = (if String.IsNullOrWhiteSpace token then None else Some token)
                    UsePairing = false
                }

    let submitCode () =
        let code = (text codeBox).Trim()

        if code.Length = 6 then
            codeSubmitted.Trigger code
        else
            codeIncomplete.Trigger()

    let cancelButton = Button(Content = "取消")

    do
        cancelButton.Classes.Add "ghost"
        cancelButton.Classes.Add "button-ghost"
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭连接对话框 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭连接对话框 (Esc)")
        cancelButton.Click.Add(fun _ -> cancelled.Trigger())

    let connectButton = Button(Content = "连接")

    do
        AutomationProperties.SetName(connectButton, "连接")
        connectButton.Classes.Add "primary"
        connectButton.Classes.Add "button-primary"
        AutomationProperties.SetHelpText(connectButton, "连接至主机 (Enter)")
        ToolTip.SetTip(connectButton, "连接至主机 (Enter)")
        connectButton.Click.Add(fun _ -> connectAction ())

    let footerRow =
        StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8.0)

    do
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Key.Right && connectButton.IsEnabled then
                e.Handled <- true
                connectButton.Focus NavigationMethod.Directional |> ignore)

        connectButton.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus NavigationMethod.Directional |> ignore)

        footerRow.Children.Add cancelButton
        footerRow.Children.Add connectButton

    let codeButton =
        let b = Button(Content = "提交配对码")
        b.Classes.Add "primary"
        b.Classes.Add "button-primary"
        b.Click.Add(fun _ -> submitCode ())
        b.IsVisible <- false
        b

    let toggle = Button(Content = "没有令牌？改用配对码")

    do
        toggle.Classes.Add "ghost"
        toggle.Classes.Add "button-ghost"
        toggle.HorizontalAlignment <- HorizontalAlignment.Left

    let syncToggle () =
        let text =
            if codeSection.IsVisible then
                "已有访问令牌？改用令牌"
            else
                "没有令牌？改用配对码"

        toggle.Content <- text
        AutomationProperties.SetName(toggle, text)


    let startPairing () =
        codeSection.IsVisible <- true
        tokenField.IsVisible <- false
        codeButton.IsVisible <- true
        connectButton.IsVisible <- false
        pairingHint.Text <- effectiveRules.Hint(text urlBox)

        connect.Trigger
            {
                Url = (text urlBox).Trim()
                Token = None
                UsePairing = true
            }

        codeBox.Focus() |> ignore

    let mutable normalizing = false

    let view =
        urlBox.Text <- initialUrl
        AutomationProperties.SetName(urlBox, "主机地址")
        AutomationProperties.SetName(tokenBox, "访问令牌")
        AutomationProperties.SetName(codeBox, "配对码")
        tokenBox.PasswordChar <- '●'
        codeBox.MaxLength <- 6
        codeBox.TextAlignment <- TextAlignment.Left
        codeBox.LetterSpacing <- 0.0
        TextInputOptions.SetContentType(codeBox, TextInputContentType.Digits)
        TextInputOptions.SetReturnKeyType(codeBox, TextInputReturnKeyType.Done)

        toggle.Click.Add(fun _ ->
            if not codeSection.IsVisible then
                startPairing ()
            else
                codeSection.IsVisible <- false
                codeButton.IsVisible <- false
                connectButton.IsVisible <- true
                tokenField.IsVisible <- true
                tokenBox.Focus() |> ignore

            syncToggle ())

        syncToggle ()
        let divider = Border(Height = 1.0)
        divider.Classes.Add "hairline"
        let headerPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        let titleBlock = TextBlock(Text = "连接万象主机", FontWeight = FontWeight.Medium)

        let descBlock =
            TextBlock(Text = "主机是运行万象的那台电脑：模型、密钥和对话都在它上面。输入它显示的 6 位配对码即可连上。", TextWrapping = TextWrapping.Wrap)

        descBlock.Classes.Add "muted"
        headerPanel.Children.Add titleBlock
        headerPanel.Children.Add descBlock
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        panel.Children.Add headerPanel
        panel.Children.Add urlField
        panel.Children.Add tokenField
        panel.Children.Add status
        panel.Children.Add retryButton
        panel.Children.Add divider
        panel.Children.Add toggle
        panel.Children.Add codeSection
        panel.Children.Add codeButton
        panel.Children.Add footerRow
        panel

    do
        codeBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                submitCode ())
        // 只含数字：粘贴「123 456」「123-456」时归一，凑满由组合者决定是否自动提交
        codeBox.TextChanged.Add(fun _ ->
            if not normalizing then
                let raw = text codeBox
                let digits = effectiveRules.Normalize raw

                if digits <> raw then
                    normalizing <- true
                    codeBox.Text <- digits
                    codeBox.CaretIndex <- digits.Length
                    normalizing <- false

                codeBox.LetterSpacing <- effectiveRules.LetterSpacing digits
                codeChanged.Trigger digits)

        urlBox.TextChanged.Add(fun _ -> pairingHint.Text <- effectiveRules.Hint(text urlBox))

        for b in [ urlBox; tokenBox ] do
            b.KeyDown.Add(fun e ->
                if e.Key = Key.Enter then
                    e.Handled <- true
                    connectAction ())

    member _.View = view :> Control
    member _.UrlBox = urlBox
    member _.TokenBox = tokenBox
    member _.CodeBox = codeBox
    member _.ConnectButton = connectButton :> Control
    member _.CancelButton = cancelButton :> Control
    member _.CodeButton = codeButton :> Control
    member _.PairingToggle = toggle :> Control
    member _.RetryButton = retryButton :> Control
    member _.StatusText = status.Text
    member _.IsPairing = codeSection.IsVisible
    member _.Code = (text codeBox).Trim()

    /// 首焦：地址空则地址，否则令牌
    member _.InitialFocus =
        (if String.IsNullOrWhiteSpace(text urlBox) then
             urlBox
         else
             tokenBox)
        :> Control

    /// 中性提示：不出现重试键
    member _.Announce(message: string) =
        retryButton.IsVisible <- false
        showMessage message false

    /// 失败：红字；retryLabel 有值时显示重试键；refocusCode 时全选并聚焦配对码
    member _.Fail(message: string, retryLabel: string option, refocusCode: bool) =
        showMessage message true

        match retryLabel with
        | Some label when not refocusCode ->
            retryButton.Content <- label
            AutomationProperties.SetName(retryButton, label)
            retryButton.IsVisible <- true
        | _ -> retryButton.IsVisible <- false

        if refocusCode then
            codeBox.Focus() |> ignore
            codeBox.SelectAll()

    /// 切到配对码（已在配对模式则不动）
    member _.StartPairing() =
        if not codeSection.IsVisible then
            startPairing ()
            syncToggle ()

    member _.Connect() = connectAction ()
    member _.SubmitCode() = submitCode ()
    member _.ClearToken() = tokenBox.Text <- ""

    member this.Render(vs: DialogVS) =
        match vs.Connect with
        | Some c ->
            if urlBox.Text <> c.DefaultUrl && not (String.IsNullOrEmpty c.DefaultUrl) then
                urlBox.Text <- c.DefaultUrl

            if c.IsPairing && not codeSection.IsVisible then
                this.StartPairing()
            elif not c.IsPairing && codeSection.IsVisible then
                codeSection.IsVisible <- false
                codeButton.IsVisible <- false
                connectButton.IsVisible <- true
                tokenField.IsVisible <- true
                syncToggle ()

            status.Text <- c.StatusText
            status.IsVisible <- c.StatusVisible

            match c.RetryLabel with
            | Some l ->
                retryButton.Content <- l
                retryButton.IsVisible <- c.RetryVisible
            | None -> retryButton.IsVisible <- false
        | None -> ()

    [<CLIEvent>]
    member _.ConnectRequested = connect.Publish

    [<CLIEvent>]
    member _.CodeSubmitted = codeSubmitted.Publish

    /// 提交时不足 6 位
    [<CLIEvent>]
    member _.CodeIncomplete = codeIncomplete.Publish

    /// 归一后的配对码（每次改动）
    [<CLIEvent>]
    member _.CodeChanged = codeChanged.Publish

    [<CLIEvent>]
    member _.RetryClicked = retry.Publish

    [<CLIEvent>]
    member _.Cancelled = cancelled.Publish
