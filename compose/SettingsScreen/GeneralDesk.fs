namespace Wanxiang2.SettingsScreen

open System
open Avalonia.Controls
open Avalonia.Threading
open Avalonia.Input.Platform
open Wanxiang.UI
open Wanxiang.Core
open Wanxiang2.Theme
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.FieldCheck
open Wanxiang2.GeneralPane

/// 设置 → 对话默认值、外观与交互、关于的协作（旧 Views/SettingsGeneral.fs）：生成参数草稿（GenerationDraft）与校验保存
/// （GenerationForm + FieldCheck）、对话预设列表、本机偏好（UiPrefs）、本机草稿（LocalDraftControls）、主机状态（HostHealth）、
/// 诊断报告复制。界面归 GeneralPane 的三个分区。
type GeneralDesk(theme: ThemeManager, overlay: OverlayDesk, hooks: unit -> SettingsHooks, initialPrefs: UiPrefs, onPrefsChanged: UiPrefs -> unit) =
    let check = FieldCheck(SettingsLooks.fieldCheckOf theme)
    let generation = GenerationSection((fun box -> Some(check.MessageFor box :> Control)), SettingsLooks.glyphOf theme)
    let appearance = AppearanceSection(SettingsLooks.glyphOf theme)
    let about = AboutSection(SettingsLooks.glyphOf theme)
    let health = HostHealth()
    let localDrafts = LocalDraftControls()
    let draft = GenerationDraft<GenerationInfo>(Catalog.empty.generation)
    let mutable catalog = Catalog.empty
    let mutable prefs = initialPrefs
    let mutable instanceId = ""
    let mutable serverUrl = ""
    let mutable presetRename: PresetInfo -> unit = ignore
    let mutable presetDelete: PresetInfo -> unit = ignore
    let mutable importHandler: (unit -> unit) option = None
    let toast msg tone = overlay.Toast(msg, tone)

    let syncStatus () = generation.SetStatus draft.Status
    let setPending (pending: bool) = generation.SetPending(pending, draft.CanSave)

    let fill (g: GenerationInfo) =
        generation.Fill
            { Temperature = GenerationForm.floatText g.temperature
              TopP = GenerationForm.floatText g.topP
              MaxTokens = (match g.maxTokens with Some v -> string v | None -> "")
              ContextMessages = string g.maxContextMessages
              ToolRounds = string g.maxToolRounds
              Instructions = g.instructions |> Option.defaultValue ""
              AutoTitle = g.autoTitle }
        syncStatus ()

    let renderPresets () =
        generation.SetPresets [ for p in catalog.presets -> { Id = p.id; Name = p.name; Detail = Presets.describe catalog p } ]

    let saveGeneration () =
        if draft.CanSave then
            for box in generation.NumberBoxes do check.Clear box
            generation.Summary.IsVisible <- false
            generation.Summary.Text <- ""
            let t = generation.Read()
            match GenerationForm.validate t.Temperature t.TopP t.MaxTokens t.ContextMessages t.ToolRounds with
            | Ok values ->
                let instructions = GenerationForm.instructions t.Instructions
                let payload = GenerationForm.payload values instructions t.AutoTitle
                let submitted =
                    { catalog.generation with
                        temperature = values.temperature
                        topP = values.topP
                        maxTokens = values.maxTokens
                        instructions = instructions
                        maxContextMessages = values.maxContextMessages
                        maxToolRounds = values.maxToolRounds
                        autoTitle = t.AutoTitle }
                let save = draft.BeginSave submitted
                setPending true
                syncStatus ()
                (hooks ()).UpdateGeneration payload (fun ok ->
                    let failed = draft.Finish(save, ok)
                    // 成功且未过期时 Baseline 就是这次提交的那份
                    if ok && obj.ReferenceEquals(draft.Baseline, save.submitted) then
                        catalog <- { catalog with generation = submitted }
                    setPending draft.Saving
                    syncStatus ()
                    if failed then toast "保存失败，请重试。" ToastTone.Failure)
            | Error errs ->
                let boxOf field =
                    match field with
                    | GenerationField.Temperature -> generation.TemperatureBox
                    | GenerationField.TopP -> generation.TopPBox
                    | GenerationField.MaxTokens -> generation.MaxTokensBox
                    | GenerationField.ContextMessages -> generation.ContextBox
                    | GenerationField.ToolRounds -> generation.ToolRoundsBox
                let errors = errs |> List.map (fun (f, m) -> boxOf f, m)
                for box, m in errors do check.SetError(box, m)
                FieldCheck.Apply(errors, Some generation.Summary) |> Option.iter (fun m -> toast m ToastTone.Warning)

    let discard () =
        if not draft.Saving then
            renderPresets ()
            draft.Discard(catalog.generation, fun () -> fill catalog.generation) |> ignore
            for box in generation.NumberBoxes do check.Clear box
            generation.Summary.IsVisible <- false
            syncStatus ()
            setPending false

    let renderAppearance () =
        appearance.Render
            { ThemeLabel = ThemePreference.label prefs.theme
              FontSizeText = sprintf "%.1f pt" prefs.fontScale
              CanShrink = prefs.fontScale > UiPrefs.minFontScale + 0.01
              CanGrow = prefs.fontScale < UiPrefs.maxFontScale - 0.01
              EnterSends = prefs.enterSends
              CollapseReasoning = prefs.autoCollapseReasoning
              CodeWrap = prefs.codeWrap
              ShowArchived = prefs.showArchived
              ReduceMotion = prefs.reduceMotion
              PersistDrafts = prefs.persistDrafts
              DraftSummary = localDrafts.Summary }

    let changePrefs next =
        prefs <- next
        renderAppearance ()
        onPrefsChanged prefs

    let platformText () =
        if OperatingSystem.IsBrowser() then "WebAssembly (PWA)" else sprintf "Desktop (.NET %s)" (Environment.Version.ToString())

    let orUnset (s: string) = if String.IsNullOrWhiteSpace s then "未连接" else s

    let renderAbout () =
        about.SetRows
            [ "协议版本", string Constants.ProtocolVersion
              "日志格式版本", string Constants.FormatVersion
              "客户端平台", platformText ()
              "操作系统", Environment.OSVersion.ToString()
              "主机实例", orUnset instanceId
              "连接地址", orUnset serverUrl ]

    let renderHealth () =
        about.SetHealth(
            health.ConfigIssue,
            match health.Doctor with
            | NotRun -> AboutDoctor.NotRun
            | Running -> AboutDoctor.Running
            | Unavailable reason -> AboutDoctor.Unavailable reason
            | Report(ok, checks, at) ->
                AboutDoctor.Report(
                    ok,
                    [ for c in checks -> { Name = c.name; Label = DoctorCheck.label c.name; Ok = c.ok; Detail = c.detail } ],
                    at.ToString "HH:mm:ss"))

    let diagnostics () =
        sprintf "万象系统诊断报告\n- 协议版本: %d\n- 日志格式: %d\n- 运行环境: %s\n- 操作系统: %s\n- 实例标识: %s\n- 连接端点: %s\n- 报告时间: %s"
            Constants.ProtocolVersion
            Constants.FormatVersion
            (platformText ())
            (Environment.OSVersion.ToString())
            (orUnset instanceId)
            (orUnset serverUrl)
            (DateTimeOffset.UtcNow.ToString("O"))

    let copyDiagnostics () =
        // 先解析剪贴板：拿不到就直说失败，成功提示只在复制完成后发
        let failed () = toast "无法访问剪贴板，诊断报告未复制。" ToastTone.Failure
        match TopLevel.GetTopLevel about.View with
        | null -> failed ()
        | top ->
            match top.Clipboard with
            | null -> failed ()
            | clip ->
                let text = diagnostics ()
                async {
                    try
                        do! clip.SetTextAsync text |> Async.AwaitTask
                        toast "诊断报告已复制到剪贴板。" ToastTone.Success
                    with _ ->
                        toast "复制诊断报告失败，请重试。" ToastTone.Failure
                }
                |> Async.Start

    do
        // 回填也会发 Edited；GenerationDraft 在回填回调里忽略它（与旧实现相同）
        generation.Edited.Add(fun () ->
            draft.Edited()
            syncStatus ())
        generation.SaveRequested.Add saveGeneration
        generation.DiscardRequested.Add discard
        generation.PresetRenameClicked.Add(fun id -> catalog.presets |> List.tryFind (fun p -> p.id = id) |> Option.iter (fun p -> presetRename p))
        generation.PresetDeleteClicked.Add(fun id -> catalog.presets |> List.tryFind (fun p -> p.id = id) |> Option.iter (fun p -> presetDelete p))
        generation.ImportClicked.Add(fun () -> importHandler |> Option.iter (fun h -> h ()))
        generation.SetImportVisible false
        appearance.ThemeClicked.Add(fun anchor ->
            overlay.ShowGroupedMenu(
                anchor,
                false,
                [ "主题",
                  [ FollowSystem; AlwaysLight; AlwaysDark ]
                  |> List.map (fun option ->
                      PopupEntry.create (ThemePreference.label option) (fun () -> changePrefs { prefs with theme = option })
                      |> PopupEntry.markSelected (prefs.theme = option)) ]))
        appearance.FontStepped.Add(fun delta ->
            let next = Math.Clamp(prefs.fontScale + delta, UiPrefs.minFontScale, UiPrefs.maxFontScale)
            if abs (next - prefs.fontScale) > 0.01 then changePrefs { prefs with fontScale = next }
            else renderAppearance ())
        appearance.Toggled.Add(fun (which, v) ->
            let next =
                match which with
                | AppearanceSwitch.EnterSends -> { prefs with enterSends = v }
                | AppearanceSwitch.CollapseReasoning -> { prefs with autoCollapseReasoning = v }
                | AppearanceSwitch.CodeWrap -> { prefs with codeWrap = v }
                | AppearanceSwitch.ShowArchived -> { prefs with showArchived = v }
                | AppearanceSwitch.ReduceMotion -> { prefs with reduceMotion = v }
                | AppearanceSwitch.PersistDrafts -> { prefs with persistDrafts = v }
            if next <> prefs then changePrefs next)
        // 确认框由宿主弹；这里只发出请求
        appearance.ClearDraftsClicked.Add(fun () -> localDrafts.RequestClear())
        localDrafts.Changed.Add(fun () -> renderAppearance ())
        about.CopyDiagnosticsClicked.Add copyDiagnostics
        about.CheckHostClicked.Add(fun () ->
            match health.Doctor with
            | Running -> ()
            | _ -> health.BeginCheck())
        health.Changed.Add(fun () -> renderHealth ())
        syncStatus ()
        setPending false
        renderPresets ()
        renderAppearance ()
        renderAbout ()
        renderHealth ()

    member _.GenerationView = generation.View
    member _.AppearanceView = appearance.View
    member _.AboutView = about.View
    member _.Generation = generation
    member _.Appearance = appearance
    member _.About = about

    /// 主机状态与「检查主机」的状态源（宿主写入，关于页订阅）
    member _.Health = health
    /// 「本机草稿」与宿主之间的桥（清除请求与概况）
    member _.LocalDrafts = localDrafts
    member _.HasUnsavedGeneration = draft.Dirty
    member _.Prefs = prefs
    member _.DiagnosticsText = diagnostics ()

    member _.SetCatalog(next: Catalog) =
        catalog <- next
        renderPresets ()
        // 目录更新也会由其它设置操作触发；不能覆盖尚未提交的表单
        draft.CatalogArrived(next.generation, fun () -> fill next.generation)
        syncStatus ()

    /// 只保留一份可见草稿；切换目标后阻止把旧主机的表单误写给新主机
    member _.SetConnection(instance: string, url: string) =
        instanceId <- instance
        serverUrl <- url
        draft.SetScope instance
        setPending draft.Saving
        syncStatus ()
        renderAbout ()

    member _.SetPrefs(next: UiPrefs) =
        prefs <- next
        renderAppearance ()

    member _.SetImportHandler(handler: unit -> unit) =
        importHandler <- Some handler
        generation.SetImportVisible true

    member _.SetPresetHandlers(rename: PresetInfo -> unit, delete: PresetInfo -> unit) =
        presetRename <- rename
        presetDelete <- delete

    member _.PresetsPanel = generation.PresetsPanel
    member _.SaveGeneration() = saveGeneration ()
    member _.DiscardGenerationChanges() = discard ()
