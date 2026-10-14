namespace Wanxiang2.SettingsScreen

open System
open System.Text.Json.Nodes
open Avalonia.Controls
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemeMetrics
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.FieldCheck
open Wanxiang2.IconSet
open Wanxiang2.ProvidersPane

/// 当前打开的模型服务编辑器（测试与宿主可读）
type ProviderEditorSession =
    { Editor: ProviderEditor
      ProviderId: string option }

/// 设置 → 模型服务的协作（旧 Views/SettingsProviders.fs）：列表行状态、预设套用与分歧判定、传输切换、
/// 探活归属与结果缓存（ProbeSlot）、校验（ProviderForm + FieldCheck）、保存往返、更多菜单。界面归 ProvidersPane。
type ProvidersDesk(theme: ThemeManager, overlay: OverlayDesk, hooks: unit -> SettingsHooks) =
    let look = SettingsLooks.providersOf theme
    let pane = ProvidersPane(look)
    let check = FieldCheck(SettingsLooks.fieldCheckOf theme)
    let probeSlot = ProbeSlot()
    let mutable catalog = Catalog.empty
    let mutable session: ProviderEditorSession option = None
    /// 探活挂钩：ApplyProbe 到达时清 pending、回填模型、写常驻失败行
    let mutable probeEditor: ((bool -> unit) * (string list -> unit) * (string -> unit)) option = None
    let icon g = fun brush -> IconSet.image g brush (theme.Space Space.GlyphMd)

    let toast msg tone = overlay.Toast(msg, tone)

    let presetNeedsKey (baseUrl: string) =
        match ProviderPresets.matchByBaseUrl baseUrl with
        | Some p -> p.needsApiKey
        | None -> true

    let configured (p: ProviderInfo) = p.hasApiKey || not (presetNeedsKey p.baseUrl) || ProviderForm.isLoopbackUrl p.baseUrl

    let rowOf (p: ProviderInfo) : ProviderRow =
        let ok = configured p
        { Id = p.id
          Label = p.label
          Meta = sprintf "%s · %d 个模型%s" p.baseUrl (List.length p.models) (if ok then "" else " · 未设置密钥")
          Enabled = p.enabled
          Dot = (if not p.enabled then ProviderDot.Disabled elif ok then ProviderDot.Configured else ProviderDot.MissingKey)
          IsDefault = Catalog.isGlobalDefault p.id catalog }

    let transportLabel kind =
        match kind with
        | "anthropic" -> "Anthropic 原生 · Messages API（支持思维链与 prompt caching）"
        | "gemini" -> "Google 原生 · generateContent（支持多模态与 systemInstruction）"
        | _ -> "OpenAI 兼容 · /v1/chat/completions"

    let text (box: TextBox) = if isNull box.Text then "" else box.Text

    let rec showEditor (existing: ProviderInfo option) =
        let editor = ProviderEditor(look, existing.IsNone, (fun box -> Some(check.MessageFor box :> Control)))
        let mutable active = true
        let takenIds = catalog.providers |> List.map (fun p -> p.id)
        let initialPreset =
            match existing with
            | Some p -> ProviderPresets.matchByBaseUrl p.baseUrl
            | None -> ProviderPresets.tryFind "openai"
        let mutable kind =
            match existing with
            | Some p when not (String.IsNullOrWhiteSpace p.kind) -> p.kind
            | _ -> initialPreset |> Option.map (fun x -> x.kind) |> Option.defaultValue "openai"
        let mutable current = initialPreset
        let mutable applying = false
        let syncKind () =
            editor.SetKindText("传输：" + transportLabel kind)
            match current with
            | Some p when p.alternate.IsSome -> editor.SetTransport(Some(if kind = p.kind then "改用兼容端点" else "改用原生接口"))
            | _ -> editor.SetTransport None
        let applyPreset (preset: ProviderPreset) =
            applying <- true
            try
                kind <- preset.kind
                current <- Some preset
                editor.UrlBox.Text <- preset.baseUrl
                editor.ModelsBox.Text <- String.Join("\n", preset.models)
                editor.DefaultModelBox.Text <- preset.models |> List.tryHead |> Option.defaultValue ""
                editor.TimeoutBox.Text <- "120"
                editor.RetriesBox.Text <- "2"
                editor.SetPresetHint preset.hint
                editor.SetPresetLabel preset.label
                syncKind ()
                if existing.IsNone then
                    editor.IdBox.Text <- ProviderPresets.suggestId preset takenIds
                    editor.LabelBox.Text <- preset.label
            finally
                applying <- false
        /// 预设一旦被手改就不再是预设：按钮文案回到「自定义」，hint 清空
        let markCustomIfDiverged () =
            if not applying then
                match current with
                | Some p ->
                    let norm =
                        (text editor.ModelsBox).Split([| '\n'; '\r'; ',' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.map (fun s -> s.Trim())
                        |> Array.filter (String.IsNullOrWhiteSpace >> not)
                        |> Array.distinct
                    let normPreset = p.models |> List.map (fun s -> s.Trim()) |> List.filter (String.IsNullOrWhiteSpace >> not) |> Array.ofList
                    let url = ProviderPresets.normalizeBaseUrl ((text editor.UrlBox).Trim())
                    let same =
                        url = ProviderPresets.normalizeBaseUrl p.baseUrl
                        || (match p.alternate with
                            | Some(_, alt) -> url = ProviderPresets.normalizeBaseUrl alt
                            | None -> false)
                    if not same || norm <> normPreset then
                        current <- None
                        editor.SetPresetHint ""
                        editor.SetPresetLabel "自定义"
                | None -> ()
        let presetEntry (preset: ProviderPreset) =
            PopupEntry.create preset.label (fun () -> applyPreset preset)
            |> PopupEntry.markSelected (current = Some preset)
        editor.PresetClicked.Add(fun anchor ->
            overlay.ShowGroupedMenu(
                anchor,
                false,
                [ "常用", ProviderPresets.featured |> List.map presetEntry
                  "更多", ProviderPresets.more |> List.map presetEntry ]))
        editor.TransportClicked.Add(fun () ->
            match current with
            | Some preset when preset.alternate.IsSome ->
                let altKind, altUrl = preset.alternate.Value
                applying <- true
                try
                    if kind = preset.kind then
                        kind <- altKind
                        editor.UrlBox.Text <- altUrl
                    else
                        kind <- preset.kind
                        editor.UrlBox.Text <- preset.baseUrl
                finally
                    applying <- false
                syncKind ()
            | _ -> ())
        editor.SetPresetLabel(match initialPreset with Some p -> p.label | None -> "自定义 OpenAI 兼容端点")
        editor.UrlBox.TextChanged.Add(fun _ -> markCustomIfDiverged ())
        editor.ModelsBox.TextChanged.Add(fun _ -> markCustomIfDiverged ())
        FieldCheck.BindSummaryReset(Some editor.Summary, editor.Boxes)
        // 看图能力：当前判断与手动指定
        match existing with
        | Some p ->
            let auto =
                ProviderProbeText.visionSummary p.models (fun m ->
                    let support, source = Catalog.visionOf p.id m catalog
                    (match support with
                     | VisionYes -> VisionVerdict.Yes
                     | VisionNo -> VisionVerdict.No
                     | VisionUnknown -> VisionVerdict.Unknown),
                    (match source with
                     | VisionFromUser -> VisionWhy.User
                     | VisionFromProvider -> VisionWhy.Provider
                     | VisionFromName -> VisionWhy.Name))
            editor.SetVisionCurrent(if String.IsNullOrWhiteSpace auto then "保存后点「获取模型列表」，能读到模型服务提供的看图信息。" else "当前判断：\n" + auto)
            editor.VisionYesBox.Text <- p.visionOverrides |> Map.filter (fun _ v -> v) |> Map.keys |> String.concat "\n"
            editor.VisionNoBox.Text <- p.visionOverrides |> Map.filter (fun _ v -> not v) |> Map.keys |> String.concat "\n"
            editor.IdBox.Text <- p.id
            editor.LockId()
            editor.LabelBox.Text <- p.label
            editor.UrlBox.Text <- p.baseUrl
            let models = probeSlot.EffectiveModels(p.id, p.models)
            editor.ModelsBox.Text <- String.Join("\n", models)
            editor.DefaultModelBox.Text <- ProbeSlot.DefaultModel(p.defaultModel, models)
            editor.TimeoutBox.Text <- string p.timeoutSeconds
            editor.RetriesBox.Text <- string p.maxRetries
            editor.KeyBox.PlaceholderText <- if p.hasApiKey then "已保存（留空则不改动）" else "粘贴密钥"
            editor.SetPresetHint(initialPreset |> Option.map (fun x -> x.hint) |> Option.defaultValue "")
            editor.SetEnabledValue p.enabled
        | None ->
            editor.SetVisionCurrent "保存后点「获取模型列表」，能读到模型服务提供的看图信息。"
            initialPreset |> Option.iter applyPreset
        syncKind ()
        editor.ProbeClicked.Add(fun () ->
            let id = (text editor.IdBox).Trim()
            if String.IsNullOrWhiteSpace id then toast "先填写稳定标识再探测。" ToastTone.Warning
            elif not (List.contains id takenIds) then toast "先保存模型服务，然后再探测模型列表。" ToastTone.Warning
            else
                check.Clear editor.UrlBox
                editor.SetProbeStatus ""
                editor.SetProbePending true
                probeSlot.Open id
                probeEditor <-
                    Some(
                        editor.SetProbePending,
                        (fun models ->
                            editor.ModelsBox.Text <- String.Join("\n", models)
                            let d = (text editor.DefaultModelBox).Trim()
                            if String.IsNullOrWhiteSpace d || not (List.contains d models) then
                                editor.DefaultModelBox.Text <- models |> List.tryHead |> Option.defaultValue ""),
                        editor.SetProbeStatus)
                (hooks ()).ProbeProvider id
                toast "正在向模型服务请求模型列表…" ToastTone.Info)
        editor.SaveRequested.Add(fun () ->
            for box in editor.Boxes do check.Clear box
            editor.Summary.IsVisible <- false
            editor.Summary.Text <- ""
            let r =
                ProviderForm.validate
                    { isNew = existing.IsNone
                      takenIds = takenIds
                      existing = existing |> Option.map (fun p -> p.timeoutSeconds, p.maxRetries)
                      id = text editor.IdBox
                      label = text editor.LabelBox
                      url = ProviderPresets.normalizeBaseUrl (text editor.UrlBox)
                      models = text editor.ModelsBox
                      defaultModel = text editor.DefaultModelBox
                      timeout = text editor.TimeoutBox
                      retries = text editor.RetriesBox
                      visionYes = text editor.VisionYesBox
                      visionNo = text editor.VisionNoBox }
            match r.autoPicked with
            | Some m ->
                editor.DefaultModelBox.Text <- m
                toast (ProviderForm.autoPickedNote m) ToastTone.Info
            | None -> ()
            let boxOf field =
                match field with
                | ProviderField.Id -> editor.IdBox
                | ProviderField.Label -> editor.LabelBox
                | ProviderField.Url -> editor.UrlBox
                | ProviderField.Models -> editor.ModelsBox
                | ProviderField.DefaultModel -> editor.DefaultModelBox
                | ProviderField.Timeout -> editor.TimeoutBox
                | ProviderField.Retries -> editor.RetriesBox
                | ProviderField.VisionYes -> editor.VisionYesBox
                | ProviderField.VisionNo -> editor.VisionNoBox
            let errors = r.errors |> List.map (fun (f, m) -> boxOf f, m)
            if errors.IsEmpty then
                let key = text editor.KeyBox
                let payload =
                    ProviderPresets.payload r.id r.label kind r.url (if isNull editor.KeyBox.Text then None else Some(key.Trim()))
                        r.models r.defaultModel editor.Enabled r.timeout r.retries
                let overrides = JsonObject()
                for m in r.visionYes do overrides[m] <- true
                for m in r.visionNo do overrides[m] <- false
                payload["visionOverrides"] <- overrides
                let needsKey =
                    match ProviderPresets.matchByBaseUrl r.url with
                    | Some preset -> preset.needsApiKey
                    | None -> not (ProviderForm.isLoopbackUrl r.url)
                let hadKey = existing |> Option.map (fun p -> p.hasApiKey) |> Option.defaultValue false
                if String.IsNullOrWhiteSpace key && needsKey && not hadKey then
                    toast "尚未填写 API Key：已先保存，在模型服务列表点「编辑」补上后才能对话。" ToastTone.Info
                editor.SetPending true
                (hooks ()).UpsertProvider payload (fun ok ->
                    editor.SetPending false
                    if ok && active then overlay.CloseDialog()
                    elif not ok then toast "保存失败，请重试。" ToastTone.Failure)
            else
                // 错误落在收起的高级区：先展开，便于看到并聚焦
                if not editor.IsAdvanced && errors |> List.exists (fun (b, _) -> List.contains b editor.AdvancedBoxes) then
                    editor.SetAdvanced true
                for box, m in errors do check.SetError(box, m)
                FieldCheck.Apply(errors, Some editor.Summary) |> Option.iter (fun m -> toast m ToastTone.Warning))
        editor.CancelRequested.Add(fun () -> overlay.CloseDialog())
        let height = (theme.Metric Metric.DialogContentMaxHeight).Subscribe(fun h -> (editor.View :?> ScrollViewer).MaxHeight <- h)
        session <- Some { Editor = editor; ProviderId = existing |> Option.map (fun p -> p.id) }
        overlay.ShowDialog(
            editor.View,
            (theme.Metric Metric.SettingsDialogWidth).Value,
            onClosed = (fun () ->
                active <- false
                height.Dispose()
                probeSlot.Close()
                probeEditor <- None
                session <- None),
            canDismiss = (fun () -> not editor.IsPending),
            initialFocus = editor.InitialFocus)

    let setGeneration (o: JsonObject) = (hooks ()).UpdateGeneration o ignore

    let moreMenu (id: string, anchor: Control) =
        match catalog.providers |> List.tryFind (fun p -> p.id = id) with
        | None -> ()
        | Some p ->
            let isDefault = Catalog.isGlobalDefault p.id catalog
            overlay.ShowMenu(
                anchor,
                true,
                [ PopupEntry.create "获取模型列表" (fun () ->
                      toast "正在向模型服务请求模型列表…" ToastTone.Info
                      (hooks ()).ProbeProvider p.id)
                  |> PopupEntry.withIcon (icon Glyph.Refresh)
                  (if isDefault then
                       PopupEntry.create "取消默认" (fun () ->
                           let o = JsonObject()
                           o["defaultProvider"] <- ""
                           o["defaultModel"] <- ""
                           setGeneration o)
                   else
                       PopupEntry.create "设为默认模型服务" (fun () ->
                           let o = JsonObject()
                           o["defaultProvider"] <- p.id
                           o["defaultModel"] <- p.defaultModel
                           setGeneration o))
                  PopupEntry.create (if p.enabled then "停用" else "启用") (fun () ->
                      (hooks ()).UpsertProvider
                          (ProviderPresets.payload p.id p.label p.kind p.baseUrl None p.models p.defaultModel (not p.enabled) p.timeoutSeconds p.maxRetries)
                          ignore)
                  PopupEntry.create "删除" (fun () -> (hooks ()).DeleteProvider p.id)
                  |> PopupEntry.withIcon (icon Glyph.Trash)
                  |> PopupEntry.asDanger ])

    do
        pane.AddClicked.Add(fun () -> showEditor None)
        pane.EditClicked.Add(fun id -> catalog.providers |> List.tryFind (fun p -> p.id = id) |> Option.iter (Some >> showEditor))
        pane.MoreClicked.Add moreMenu
        pane.Render []

    member _.View = pane.View
    member _.Pane = pane
    member _.Session = session

    member _.SetCatalog(next: Catalog) =
        catalog <- next
        pane.Render(next.providers |> List.map rowOf)

    /// 直接打开「添加模型服务」编辑器（首次接入、错误卡直达）
    member _.OpenAddEditor() = showEditor None

    /// 打开某个已有服务的编辑器
    member _.OpenEditor(providerId: string) =
        catalog.providers |> List.tryFind (fun p -> p.id = providerId) |> Option.iter (Some >> showEditor)

    /// 探活结果：回填打开中的编辑器草稿（不自动保存），否则只缓存并提示
    member _.ApplyProbe(providerId: string, ok: bool, models: string list, error: string option) =
        let message = if ok then "" else snd (ProbeFailureKind.parse (error |> Option.defaultValue ProviderProbeText.unknownReason))
        let editor = if probeSlot.Claim providerId then probeEditor else None
        let fb = ProviderProbeText.feedback editor.IsSome ok (List.length models) message
        match editor with
        | Some(setPending, apply, setStatus) ->
            setPending false
            fb.editorStatus |> Option.iter setStatus
            if fb.fillModels then apply models
        | None -> ()
        if fb.cacheModels then probeSlot.Remember(providerId, models)
        toast fb.toast (match fb.tone with
                        | ProbeTone.Info -> ToastTone.Info
                        | ProbeTone.Warning -> ToastTone.Warning
                        | ProbeTone.Failure -> ToastTone.Failure)
