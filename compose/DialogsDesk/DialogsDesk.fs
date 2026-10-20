namespace Wanxiang2.DialogsDesk

open System
open System.Globalization
open Avalonia.Controls
open Avalonia.Threading
open Wanxiang.Core
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.FieldCheck
open Wanxiang2.ConfirmPane
open Wanxiang2.PromptPane
open Wanxiang2.TextEditPane
open Wanxiang2.ConnectPane
open Wanxiang2.SessionPrefsPane
open Wanxiang2.ShortcutHelpPane

/// 连接对话框交给宿主的入口（旧 ConnectDialogHandle）：Info 报中性进度，Fail 报类型化失败，
/// StartPairing 直接进入配对码模式；Pane 供宿主/测试读状态。
type ConnectDeskHandle =
    { Info: string -> unit
      Fail: ConnectionIssue -> unit
      StartPairing: unit -> unit
      Pane: ConnectPane }

/// 旧 Views/Dialogs.fs 的协作：把各面板摆进 OverlayDesk，接确认/取消/校验/保存往返。
/// 界面全部在面板库里；这里只做关闭、焦点、提示条与领域换算。
type DialogsDesk(theme: ThemeManager, overlay: OverlayDesk) =
    let check = FieldCheck()
    let width m = (theme.Metric m).Value
    let validationFor = fun (box: TextBox) -> Some(check.MessageFor box :> Control)

    /// 旧 shortcuts 的分组文案（逐字保留）
    static member ShortcutSections =
        [ "全局与导航",
          [ "Ctrl / ⌘ + N", "新建对话"
            "Ctrl / ⌘ + W", "关闭窗口"
            "Ctrl / ⌘ + B", "切换侧边栏展开 / 折叠"
            "Ctrl / ⌘ + K", "命令面板：搜对话、操作与设置"
            "Ctrl / ⌘ + Shift + F", "在侧栏搜索全部消息"
            "Ctrl / ⌘ + 1 ~ 9", "按侧栏当前可见顺序快速跳转至对应对话"
            "Ctrl / ⌘ + ,", "打开全局设置"
            "Ctrl / ⌘ + M", "切换本对话使用的模型"
            "Ctrl / ⌘ + Shift + S", "切换主题（跟随系统 → 浅色 → 深色轮转）"
            "Ctrl / ⌘ + Z", "撤销刚才的删除（提示条还在时）"
            "F6", "把焦点移到提示条上的按钮（如「撤销」）"
            "Ctrl / ⌘ + /", "显示快捷键帮助"
            "F1", "显示快捷键帮助" ]
          "输入与对话",
          [ "Enter", "发送消息（或换行，按偏好）"
            "Shift + Enter", "换行输入"
            "Ctrl / ⌘ + Enter", "发送 / 换行（与 Enter 互补，按当前偏好）"
            "Ctrl / ⌘ + Shift + E", "导出当前对话为 Markdown"
            "Ctrl / ⌘ + 上 / 下", "跳到上一条 / 下一条提问（顶栏同款按钮）"
            "Ctrl / ⌘ + Home", "滚到对话开头（最早的提问）"
            "Ctrl / ⌘ + End", "滚到对话结尾（最新消息）"
            "Ctrl / ⌘ + L", "聚焦消息输入框"
            "Ctrl / ⌘ + Shift + C", "复制最新一条回复"
            "Ctrl / ⌘ + Shift + G", "重新生成最新回复"
            "Esc", "关闭弹层 / 取消编辑 / 停止生成" ]
          "对话行",
          [ "Enter", "打开当前对话（或提交重命名）"
            "P", "置顶 / 取消置顶当前对话"
            "F2", "重命名当前对话"
            "Delete", "删除当前对话（多选模式下批量删除）" ] ]

    member _.FieldCheck = check

    /// 单行输入（重命名等）。onConfirm 收到去空白的值与回执；回执 true 关闭，false 留在对话框。
    member _.Prompt(title: string, hint: string, initial: string, confirmLabel: string, onConfirm: string -> (bool -> unit) -> unit) =
        let pane = PromptPane(title, hint, initial, confirmLabel, validationFor)
        let mutable active = true
        pane.FieldError.Add(fun m ->
            match m with
            | Some message -> check.ApplyError(pane.Box, message)
            | None -> check.Clear pane.Box)
        pane.Cancelled.Add(fun () -> overlay.CloseDialog())
        pane.Submitted.Add(fun value ->
            pane.ApplyPending true
            onConfirm value (fun ok ->
                if active then
                    pane.ApplyPending false
                    if ok then overlay.CloseDialog()))
        overlay.ShowDialog(
            pane.View, width Metric.ConfirmDialogWidth,
            onClosed = (fun () -> active <- false),
            canDismiss = (fun () -> not pane.IsPending))
        pane.Box.Focus() |> ignore
        pane.Box.SelectAll()
        pane

    /// 确认；dangerFirst 时首焦落在主按钮，否则落在取消（安全项）。
    member _.ConfirmWithFocus(title: string, body: string, confirmLabel: string, onConfirm: unit -> unit, dangerFirst: bool) =
        let pane = ConfirmPane(title, body, confirmLabel, dangerFirst)
        pane.Cancelled.Add(fun () -> overlay.CloseDialog())
        pane.Confirmed.Add(fun () ->
            overlay.CloseDialog()
            onConfirm ())
        overlay.ShowDialog(pane.View, width Metric.ConfirmDialogWidth, initialFocus = pane.InitialFocus)
        pane

    member this.Confirm(title: string, body: string, confirmLabel: string, onConfirm: unit -> unit) =
        this.ConfirmWithFocus(title, body, confirmLabel, onConfirm, false)

    /// 长文本编辑（编辑消息并分叉）；确认即关闭，值原样交出（不去空白，与旧实现一致）。
    member _.EditText(title: string, initial: string, confirmLabel: string, onConfirm: string -> unit) =
        let pane = TextEditPane(title, initial, confirmLabel)
        pane.Cancelled.Add(fun () -> overlay.CloseDialog())
        pane.Submitted.Add(fun value ->
            overlay.CloseDialog()
            onConfirm value)
        overlay.ShowDialog(pane.View, width Metric.SessionDialogWidth)
        pane.FocusEnd()
        pane

    /// 连接对话框：地址 + 令牌，或配对码。重试按钮由 ConnectionIssue.retry 决定，与文案无关。
    member _.ConnectWithClosed(defaultUrl: string, onConnect: ConnectAsk -> unit, onSubmitCode: string -> unit, onClosed: unit -> unit) : ConnectDeskHandle =
        let rules =
            { Normalize = PairingCodeInput.normalize
              LetterSpacing = PairingCodeInput.letterSpacing
              Hint = PairingHint.codeHint }
        let pane = ConnectPane(defaultUrl, rules)
        let autoSubmit = PairingAutoSubmit()
        let mutable pendingRetry: ConnectionRetry option = None
        let announce message =
            pendingRetry <- None
            pane.Announce message
        pane.ConnectRequested.Add onConnect
        pane.CodeSubmitted.Add onSubmitCode
        pane.CodeIncomplete.Add(fun () -> announce PairingCodeInput.incomplete)
        // 同一串 6 位码只自动提交一次，改动后才可再次触发
        pane.CodeChanged.Add(fun digits -> if autoSubmit.Changed digits then pane.SubmitCode())
        pane.Cancelled.Add(fun () -> overlay.CloseDialog())
        pane.RetryClicked.Add(fun () ->
            match pendingRetry with
            | Some RetryConnect -> pane.Connect()
            | Some RePair ->
                // 令牌已失效：清掉令牌，回到配对
                pane.ClearToken()
                if pane.IsPairing then onConnect { Url = pane.UrlBox.Text.Trim(); Token = None; UsePairing = true }
                else pane.StartPairing()
            | Some RetryPairing ->
                if pane.Code.Length = 6 then pane.SubmitCode()
                else onConnect { Url = (if isNull pane.UrlBox.Text then "" else pane.UrlBox.Text.Trim()); Token = None; UsePairing = true }
            | None -> ())
        let fail (issue: ConnectionIssue) =
            pendingRetry <- ConnectionIssue.retry issue
            let message = ConnectIssueText.describe issue
            match issue, pendingRetry with
            | ConnectionIssue.PairingRejected "invalid pairing code", _ ->
                // 码输错时配对会话仍有效：不给重试键，全选错码重输
                pane.Fail(message, None, true)
            | _, Some retry -> pane.Fail(message, Some(ConnectIssueText.retryLabel retry), false)
            | _, None -> pane.Fail(message, None, false)
        overlay.ShowDialog(pane.View, width Metric.ConnectDialogWidth, initialFocus = pane.InitialFocus, onClosed = onClosed)
        { Info = announce
          Fail = fail
          StartPairing = pane.StartPairing
          Pane = pane }

    member this.Connect(defaultUrl: string, onConnect: ConnectAsk -> unit, onSubmitCode: string -> unit) =
        this.ConnectWithClosed(defaultUrl, onConnect, onSubmitCode, ignore)

    /// 对话设置：模型、生成参数、系统指令、工具勾选；保存往返失败时报提示条并留在对话框。
    member _.SessionSettings(catalog: Catalog, current: SessionConfig, onSave: SessionConfig -> (bool -> unit) -> unit) =
        let mutable active = true
        let mutable providerId = current.provider
        let mutable model = current.model
        let mutable picker: SelectButton option = None
        let options () =
            [ for provider in Catalog.usableProviders catalog ->
                  provider.label,
                  [ for candidate in provider.models ->
                        PopupEntry.create candidate (fun () ->
                            providerId <- provider.id
                            model <- candidate
                            picker |> Option.iter (fun p -> p.ApplyText(sprintf "%s · %s" provider.label candidate)))
                        |> PopupEntry.markSelected (provider.id = providerId && candidate = model) ] ]
        let select = overlay.SelectButton(Catalog.describeModel providerId model catalog, options)
        picker <- Some select
        select.View.HorizontalAlignment <- Avalonia.Layout.HorizontalAlignment.Stretch
        let num (v: float option) = match v with Some x -> x.ToString("0.##", CultureInfo.InvariantCulture) | None -> ""
        let int' (v: int option) = match v with Some x -> string x | None -> ""
        let init =
            { SessionPrefsInit.Instructions = defaultArg current.instructions ""
              Temperature = num current.temperature
              TopP = num current.topP
              MaxTokens = int' current.maxTokens
              ThinkingBudget = int' current.thinkingBudget
              Tools =
                [ for tool in catalog.tools ->
                      { Id = tool.id
                        // 内置工具用人话名，与工具卡、设置页一致
                        Label = (if tool.source = "mcp" then tool.label else ToolSummary.displayName tool.id)
                        Source = (if tool.source = "mcp" then defaultArg tool.serverId "mcp" else "内置")
                        Enabled = List.contains tool.id current.tools } ]
              Customized = current.temperature.IsSome || current.topP.IsSome || current.maxTokens.IsSome || current.thinkingBudget.IsSome }
        let pane = SessionPrefsPane(select.View, init, validationFor)
        let boxes = [ pane.TemperatureBox; pane.TopPBox; pane.MaxTokensBox; pane.ThinkingBudgetBox ]
        FieldCheck.BindSummaryReset(Some pane.Summary, boxes)
        pane.Cancelled.Add(fun () -> overlay.CloseDialog())
        pane.SaveRequested.Add(fun draft ->
            for b in boxes do check.Clear b
            pane.ShowSummary None
            let errors = ResizeArray<TextBox * string>()
            let invalid box message =
                check.ApplyError(box, message)
                errors.Add(box, message)
            let parseFloat (box: TextBox) (raw: string) valid message =
                let raw = raw.Trim()
                if String.IsNullOrWhiteSpace raw then None
                else
                    match Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture) with
                    | true, v when valid v -> Some v
                    | _ -> invalid box message; None
            let parseInt (box: TextBox) (raw: string) valid message =
                let raw = raw.Trim()
                if String.IsNullOrWhiteSpace raw then None
                else
                    match Int32.TryParse raw with
                    | true, v when valid v -> Some v
                    | _ -> invalid box message; None
            let temperature = parseFloat pane.TemperatureBox draft.Temperature (fun v -> v >= 0.0 && v <= 2.0) "请输入 0–2 之间的数字。"
            let topP = parseFloat pane.TopPBox draft.TopP (fun v -> v >= 0.0 && v <= 1.0) "请输入 0–1 之间的数字。"
            let maxTokens = parseInt pane.MaxTokensBox draft.MaxTokens (fun v -> v > 0) "请输入正整数。"
            let thinkingBudget = parseInt pane.ThinkingBudgetBox draft.ThinkingBudget (fun v -> v >= 0) "请输入 0 或正整数。"
            let instructions = let t = draft.Instructions.Trim() in if String.IsNullOrWhiteSpace t then None else Some t
            if errors.Count = 0 then
                if not pane.IsPending then
                    pane.ApplyPending true
                    onSave
                        { current with
                            provider = providerId
                            model = model
                            instructions = instructions
                            temperature = temperature
                            topP = topP
                            maxTokens = maxTokens
                            thinkingBudget = thinkingBudget
                            tools = draft.Tools }
                        (fun ok ->
                            if active then
                                pane.ApplyPending false
                                if ok then overlay.CloseDialog()
                                else overlay.Toast("保存失败，请重试。", ToastTone.Failure))
            else
                pane.RevealParameters()
                match FieldCheck.Apply(List.ofSeq errors, Some pane.Summary) with
                | Some message -> overlay.Toast(message, ToastTone.Warning)
                | None -> ())
        overlay.ShowDialog(
            pane.View, width Metric.SessionDialogWidth,
            onClosed = (fun () -> active <- false),
            canDismiss = (fun () -> not pane.IsPending),
            // 内容自带滚动区与固定页脚：卡片按视口封顶，矮窗口里「保存」不被挤出（旧 scrollWholeContent = false）。
            scrollWholeContent = false)
        pane

    /// 快捷键帮助：唯一停留点是关闭键。
    member _.Shortcuts() =
        let pane = ShortcutHelpPane(DialogsDesk.ShortcutSections)
        pane.Closed.Add(fun () -> overlay.CloseDialog())
        overlay.ShowDialog(pane.View, width Metric.ConnectDialogWidth, initialFocus = pane.InitialFocus)
        pane
