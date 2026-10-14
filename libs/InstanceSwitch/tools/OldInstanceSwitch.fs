namespace Wanxiang.UI

// 原样摘自 AppShell.fs HandleEvent 的 `| AuthAccepted d ->` 分支（逐行，次序不变）。读到的字段换成参数，
// 每个副作用换成记下对应的 InstanceStep；连接状态、设置页等与重置无关的赋值行记为同一步或略去（见行尾注）。
[<RequireQualifiedAccess>]
type InstanceStep =
    | CloseExportDialog
    | SaveComposerDraft
    | DisconnectOutbox of previousInstance: string
    | ResetClientState
    | ClearRuns
    | ClearCatalog
    | ResetHostHealth
    | ClearActiveConversation
    | ClearConversationList
    | LoadNewInstanceDraft
    | ResetHostFeatures
    | AnnounceApprovalCapability
    | ClearConnectionIssue
    | ResetReconnectDelay
    | ClearRunErrors
    | CloseDialogs
    | RestoreLocalDrafts
    | RefreshDevicesIfShown
    | SaveBrowserConnection
    | RequestCatalog
    | ObserveConversationList
    | ObserveActiveConversation

module InstanceSwitch =
    let isSwitch (previousInstance: string) (newInstance: string) =
        previousInstance <> "" && previousInstance <> newInstance

    let plan (previousInstance: string) (newInstance: string) (exportDialogOpen: bool) (isBrowser: bool) (hasToken: bool) (hasActiveConversation: bool) : InstanceStep list =
        let steps = ResizeArray<InstanceStep>()
        let step s = if not (steps.Contains s) then steps.Add s
        let instanceId = previousInstance
        let d = {| instanceId = newInstance |}
        // ExportDialog.IsOpen 在 overlay.CloseDialog() 的 onClosed 回调里置 false（Views/ExportDialog.fs）
        let mutable exportOpen = exportDialogOpen
        let exportDialog () = if exportDialogOpen then Some {| IsOpen = exportOpen |} else None
        let lastToken = if hasToken then Some "t" else None
        let activeConvId = if hasActiveConversation then Some System.Guid.Empty else None
        if instanceId <> "" && instanceId <> d.instanceId then
            if exportDialog () |> Option.exists (fun dialog -> dialog.IsOpen) then
                step InstanceStep.CloseExportDialog // overlay.CloseDialog()
                exportOpen <- false
            step InstanceStep.SaveComposerDraft            // (drafts.Get(instanceId, activeConvId)).text <- composer.Text
            step (InstanceStep.DisconnectOutbox instanceId) // outbox.Disconnect instanceId
            step InstanceStep.ResetClientState             // state.Reset()
            step InstanceStep.ClearRuns                    // runs.Clear()
            step InstanceStep.ClearCatalog                 // catalog <- Catalog.empty; settings.SetCatalog catalog
            step InstanceStep.ResetHostHealth              // settings.Health.Reset()
            step InstanceStep.ClearActiveConversation      // activeConvId <- None
            step InstanceStep.ClearConversationList        // summaries <- []; sidebar.SetConversations []
            step InstanceStep.LoadNewInstanceDraft         // composer.SetText((drafts.Get(d.instanceId, None)).text)
        // authenticated <- true
        step InstanceStep.ResetHostFeatures                // hostFeatures <- None
        step InstanceStep.AnnounceApprovalCapability       // send (ToolApprovalCapability ...)
        step InstanceStep.ClearConnectionIssue             // hasSavedCredentials; connectionError; connectionIssue; instanceId
        step InstanceStep.ResetReconnectDelay              // reconnect.ResetDelay(); pairingRequested <- false
        step InstanceStep.ClearRunErrors                   // for s in summaries ... runs.ClearError; activeConvId |> Option.iter runs.ClearError
        if not (exportDialog () |> Option.exists (fun dialog -> dialog.IsOpen)) then step InstanceStep.CloseDialogs
        // sidebar.SetConnection / settings.SetConnection：连接状态显示，随 ClearConnectionIssue 一并由宿主更新
        step InstanceStep.RestoreLocalDrafts               // this.RestoreLocalDrafts()
        step InstanceStep.RefreshDevicesIfShown            // if settings.Devices.IsShown then settings.Devices.Refresh()
        if isBrowser then
            match lastToken with
            | Some token -> step InstanceStep.SaveBrowserConnection
            | None -> ()
        step InstanceStep.RequestCatalog                   // send CatalogRequest
        step InstanceStep.ObserveConversationList          // observeConversationList ()
        match activeConvId with
        | Some convId -> step InstanceStep.ObserveActiveConversation
        | None -> ()
        List.ofSeq steps
