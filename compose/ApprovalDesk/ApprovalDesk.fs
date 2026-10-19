namespace Wanxiang2.ApprovalDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang.Protocol
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.IconSet
open Wanxiang2.ToastStrip
open Wanxiang2.OverlayDesk
open Wanxiang2.ApprovalPanel
open Wanxiang2.WorkspaceState

/// 工具审批（第 71 节）的接线：主机事件 → 卡片；点批准 / 拒绝 → `ToolApprovalDecision`；
/// 别的对话来审批、已被处理过都用提示条告知；待批准的 callId 变了就发 `AwaitingChanged`（消息里的工具卡据此标「等待批准」）。
/// 状态只在内存里：断线清空，主机重连后重发。
type ApprovalDesk(wrenchIcon: (unit -> Control) option, overlay: OverlayDesk, send: WireEvent -> unit) =
    let awaiting = Event<Set<string>>()
    let texts =
        { PolicyNote = ApprovalCardText.policyNote
          SentText = ApprovalCardText.sentText
          DisplayArguments = ApprovalCardText.displayArguments
          ArgsToggleText = ApprovalCardText.argsToggleText }
    let panel = ApprovalPanel(texts, wrenchIcon)
    let mutable active: Guid option = None
    let sync () = awaiting.Trigger panel.AwaitingCallIds

    do
        panel.Decide.Add(fun (approvalId, approved) ->
            send (ToolApprovalDecision {| approvalId = approvalId; approved = approved |}))

    /// 从主题直接建（宿主常用的入口）
    new(theme: ThemeManager, overlay: OverlayDesk, send: WireEvent -> unit) =
        let wrench = Some(fun () -> IconSet.image Glyph.Wrench (theme.Brush PaletteKey.Warning) (theme.Metric Metric.IconGlyph))
        ApprovalDesk(wrench, overlay, send)

    /// 放进输入框正上方附属位的面板
    member _.View: Control = panel
    member _.Panel = panel
    member _.Pending = panel.Pending
    member _.AwaitingCallIds = panel.AwaitingCallIds

    /// 单入口幂等渲染：由单一状态树直接驱动
    member _.Render(vs: ApprovalVS, currentConversation: Guid option) =
        active <- currentConversation
        let items: ApprovalAsk list =
            vs.Items
            |> List.map (fun i ->
                {
                    ApprovalId = i.ApprovalId
                    ConversationId = i.ConversationId
                    ToolName = i.ToolName
                    CallId = i.CallId
                    Summary = i.Summary
                    ArgumentsJson = i.ArgumentsJson
                    Policy = i.Policy
                })
        panel.Render(items, currentConversation)
        sync ()

    /// 切换当前对话：只显示它的卡片
    member _.SetConversation(convId: Guid option) =
        active <- convId
        panel.SetConversation convId

    /// 主机事件入口；是审批事件就处理并返回 true
    member _.Handle(ev: WireEvent) : bool =
        match ev with
        | ToolApprovalRequested d ->
            panel.Upsert
                { ApprovalId = d.approvalId
                  ConversationId = d.conversationId
                  ToolName = d.toolName
                  CallId = d.callId
                  Summary = d.summary
                  ArgumentsJson = (if isNull d.arguments then "" else d.arguments.ToJsonString())
                  Policy = d.policy }
            if active <> Some d.conversationId then
                overlay.Toast(ApprovalCardText.otherConversationWaitingText, Wanxiang2.ToastStrip.ToastTone.Info)
            sync ()
            true
        | ToolApprovalResolved d ->
            panel.Remove d.approvalId
            sync ()
            if d.alreadyHandled then overlay.Toast(ApprovalCardText.alreadyHandledText d.status, Wanxiang2.ToastStrip.ToastTone.Info)
            true
        | _ -> false

    /// 断线：清空，避免显示已失效的卡片
    member _.Disconnected() =
        panel.Clear()
        sync ()

    /// 待批准的 callId 集合变了
    [<CLIEvent>] member _.AwaitingChanged = awaiting.Publish
