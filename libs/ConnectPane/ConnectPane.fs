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

/// 一次连接请求
type ConnectAsk = { Url: string; Token: string option; UsePairing: bool }

/// 配对码输入的规则（组合者从 PairingCodeInput / PairingHint 给）
type CodeRules =
    { /// 粘贴文本 → 纯数字
      Normalize: string -> string
      /// 按已输入位数给字距
      LetterSpacing: string -> float
      /// 地址 → 配对码说明
      Hint: string -> string }

/// 连接对话框的内容（迁自旧 Views/Dialogs.fs 的 connectWithClosed）：地址 + 令牌，或切到配对码。
/// 失败归类、重试种类、自动提交时机归组合者；本控件只管字段、显隐、焦点与事件。
type ConnectPane(look: ConnectPaneLook, defaultUrl: string, rules: CodeRules) =
    let kit = Kit(look)
    let connect = Event<ConnectAsk>()
    let codeSubmitted = Event<string>()
    let codeIncomplete = Event<unit>()
    let codeChanged = Event<string>()
    let retry = Event<unit>()
    let cancelled = Event<unit>()
    let text (b: TextBox) = if isNull b.Text then "" else b.Text

    let urlShell, urlBox = kit.TextField "ws://127.0.0.1:8765/ws"
    let tokenShell, tokenBox = kit.TextField "有令牌就粘贴，没有就用配对码"
    let urlField = kit.FieldGroup("主机地址", "", urlShell, None)
    let tokenField = kit.FieldGroup("访问令牌", "", tokenShell, None)
    let codeShell, codeBox = kit.TextField "6 位配对码"
    let pairingHint = kit.Caption(rules.Hint defaultUrl)
    let codeSection =
        let inner = kit.VStack(look.SpaceMd, [ kit.FieldLabel "配对码" :> Control; codeShell :> Control; pairingHint :> Control ])
        let card = kit.Card(look.SpaceXl, look.SpaceMd, look.RadiusMd, inner)
        card.IsVisible <- false
        card
    let status =
        let t = kit.Caption ""
        t.IsVisible <- false
        // 连接错误走 assertive live region
        AutomationProperties.SetLiveSetting(t, AutomationLiveSetting.Assertive)
        t
    let retryButton =
        let b = kit.Button(Tone.Secondary, "重试连接", fun () -> retry.Trigger())
        b.IsVisible <- false
        b.HorizontalAlignment <- HorizontalAlignment.Left
        b

    let showMessage (message: string) (failure: bool) =
        status.Text <- message
        let has = not (String.IsNullOrWhiteSpace message)
        status.IsVisible <- has
        AutomationProperties.SetName(status, message)
        status.Foreground <- (if failure && has then look.Danger else look.Muted)

    let connectAction () =
        let url = (text urlBox).Trim()
        let token = (text tokenBox).Trim()
        if String.IsNullOrWhiteSpace url then
            retryButton.IsVisible <- false
            showMessage "请填写主机地址。" false
        else connect.Trigger { Url = url; Token = (if String.IsNullOrWhiteSpace token then None else Some token); UsePairing = false }

    let submitCode () =
        let code = (text codeBox).Trim()
        if code.Length = 6 then codeSubmitted.Trigger code else codeIncomplete.Trigger()

    let footer = Footer(kit, "连接", Tone.Primary, (fun () -> cancelled.Trigger()), connectAction)
    let codeButton =
        let b = kit.Button(Tone.Primary, "提交配对码", submitCode)
        b.IsVisible <- false
        b
    let mutable toggle: Tap = Unchecked.defaultof<_>

    let syncToggle () =
        toggle.SetText(if codeSection.IsVisible then "已有访问令牌？改用令牌" else "没有令牌？改用配对码")

    let startPairing () =
        codeSection.IsVisible <- true
        // 配对模式下令牌框与页脚「连接」退场：只留一个主按钮
        tokenField.IsVisible <- false
        codeButton.IsVisible <- true
        footer.Confirm.IsVisible <- false
        pairingHint.Text <- rules.Hint(text urlBox)
        connect.Trigger { Url = (text urlBox).Trim(); Token = None; UsePairing = true }
        Dispatcher.UIThread.Post(fun () -> codeBox.Focus() |> ignore)

    let mutable normalizing = false

    let view =
        urlBox.Text <- defaultUrl
        AutomationProperties.SetName(urlBox, "主机地址")
        AutomationProperties.SetName(tokenBox, "访问令牌")
        AutomationProperties.SetName(codeBox, "配对码")
        tokenBox.PasswordChar <- '●'
        codeBox.MaxLength <- 6
        kit.Live(look.TitleFontSize, fun v -> codeBox.FontSize <- v)
        codeBox.TextAlignment <- TextAlignment.Left
        codeBox.LetterSpacing <- 0.0
        kit.Live(look.MonoFont, fun f -> codeBox.FontFamily <- f)
        TextInputOptions.SetContentType(codeBox, TextInputContentType.Digits)
        TextInputOptions.SetReturnKeyType(codeBox, TextInputReturnKeyType.Done)
        toggle <-
            kit.Button(Tone.Ghost, "没有令牌？改用配对码", fun () ->
                if not codeSection.IsVisible then startPairing ()
                else
                    codeSection.IsVisible <- false
                    codeButton.IsVisible <- false
                    footer.Confirm.IsVisible <- true
                    tokenField.IsVisible <- true
                    tokenBox.Focus() |> ignore
                syncToggle ())
        toggle.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetHelpText(footer.Cancel, "取消并关闭连接对话框 (Esc)")
        ToolTip.SetTip(footer.Cancel, "取消并关闭连接对话框 (Esc)")
        AutomationProperties.SetHelpText(footer.Confirm, "连接至主机 (Enter)")
        ToolTip.SetTip(footer.Confirm, "连接至主机 (Enter)")
        kit.Live(look.SpaceMd, footer.Spacing)
        let rule = kit.Hairline()
        kit.Live(look.BorderWidth, fun v -> rule.Height <- v)
        kit.VStack(
            look.Space2xl,
            [ kit.VStack(
                look.SpaceXs,
                [ kit.Title "连接万象主机" :> Control
                  kit.Caption "主机是运行万象的那台电脑：模型、密钥和对话都在它上面。输入它显示的 6 位配对码即可连上。" :> Control ])
              :> Control
              urlField
              tokenField
              status :> Control
              retryButton :> Control
              rule :> Control
              toggle :> Control
              codeSection :> Control
              codeButton :> Control
              footer.Row :> Control ])

    do
        codeBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                submitCode ())
        // 只含数字：粘贴「123 456」「123-456」时归一，凑满由组合者决定是否自动提交
        codeBox.TextChanged.Add(fun _ ->
            if not normalizing then
                let raw = text codeBox
                let digits = rules.Normalize raw
                if digits <> raw then
                    normalizing <- true
                    codeBox.Text <- digits
                    codeBox.CaretIndex <- digits.Length
                    normalizing <- false
                codeBox.LetterSpacing <- rules.LetterSpacing digits
                codeChanged.Trigger digits)
        urlBox.TextChanged.Add(fun _ -> pairingHint.Text <- rules.Hint(text urlBox))
        for b in [ urlBox; tokenBox ] do
            b.KeyDown.Add(fun e ->
                if e.Key = Key.Enter then
                    e.Handled <- true
                    connectAction ())

    member _.View = view :> Control
    member _.UrlBox = urlBox
    member _.TokenBox = tokenBox
    member _.CodeBox = codeBox
    member _.ConnectButton = footer.Confirm :> Control
    member _.CancelButton = footer.Cancel :> Control
    member _.CodeButton = codeButton :> Control
    member _.PairingToggle = toggle :> Control
    member _.RetryButton = retryButton :> Control
    member _.StatusText = status.Text
    member _.IsPairing = codeSection.IsVisible
    member _.Code = (text codeBox).Trim()

    /// 首焦：地址空则地址，否则令牌
    member _.InitialFocus = (if String.IsNullOrWhiteSpace(text urlBox) then urlBox else tokenBox) :> Control

    /// 中性提示：不出现重试键
    member _.Announce(message: string) =
        retryButton.IsVisible <- false
        showMessage message false

    /// 失败：红字；retryLabel 有值时显示重试键；refocusCode 时全选并聚焦配对码
    member _.Fail(message: string, retryLabel: string option, refocusCode: bool) =
        showMessage message true
        match retryLabel with
        | Some label when not refocusCode ->
            retryButton.SetText label
            retryButton.IsVisible <- true
        | _ -> retryButton.IsVisible <- false
        if refocusCode then
            Dispatcher.UIThread.Post(fun () ->
                codeBox.Focus() |> ignore
                codeBox.SelectAll())

    /// 切到配对码（已在配对模式则不动）
    member _.StartPairing() =
        if not codeSection.IsVisible then
            startPairing ()
            syncToggle ()

    member _.Connect() = connectAction ()
    member _.SubmitCode() = submitCode ()
    member _.ClearToken() = tokenBox.Text <- ""

    [<CLIEvent>] member _.ConnectRequested = connect.Publish
    [<CLIEvent>] member _.CodeSubmitted = codeSubmitted.Publish
    /// 提交时不足 6 位
    [<CLIEvent>] member _.CodeIncomplete = codeIncomplete.Publish
    /// 归一后的配对码（每次改动）
    [<CLIEvent>] member _.CodeChanged = codeChanged.Publish
    [<CLIEvent>] member _.RetryClicked = retry.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
