namespace Wanxiang2.SettingsScreen

open System
open System.Text.Json.Nodes
open System.Threading.Tasks
open Avalonia.Controls
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.OnboardingPane
open Wanxiang2.WorkspaceState

/// 首次运行卡片对外的出口（旧 OnboardingActions 去掉 toast）。全部落到与设置页相同的协议命令上：
/// 卡片不拥有第二套保存逻辑（载荷由 ProviderPresets.payload 构造）。
type OnboardingHooks =
    { UpsertProvider: JsonObject -> (bool -> unit) -> unit
      /// 删除（不弹确认）：只用于撤销卡片自己刚创建、但探测失败的模型服务
      DiscardProvider: string -> unit
      ProbeProvider: string -> unit
      TakenIds: unit -> string list
      /// 本机 Ollama 发现：Some 模型清单 = 在响应；None = 没有或不允许探测
      DiscoverLocal: unit -> Task<string list option>
      /// 进入设置页的完整「添加模型服务」表单
      OpenAdvanced: unit -> unit
      /// 卡片里的用户选择与发现结果回报给宿主：事实归模型，宿主投影回来（W7-B3）
      Report: OnboardingUserMsg -> unit }

/// 命令类操作登记：
/// - ApplyProbe: 探测结果入口
/// - TakeConnected: 刚连接成功且目录可用时取走 (id, 模型)
/// - StartDiscovery: 触发 Ollama 探测任务
/// - ApplyDiscovered: 发现结果到达
/// - Reset / FocusInitial: 界面复位与焦点
/// 「连接一个模型」卡片的协作（旧 Views/OnboardingCard.fs）：所选预设、步骤与本次尝试（OnboardingFlow）、
/// 兼容端点切换（ProbeFailureKind）、两次写回（先存再按探到的清单与默认模型落盘）、本机 Ollama 一键接入。界面归 OnboardingPane。
type OnboardingDesk(_theme: ThemeManager, overlay: OverlayDesk, hooks: OnboardingHooks) =
    let pane = OnboardingPane()
    let flow = OnboardingFlow()
    let selectable = ProviderPresets.all |> List.filter (fun p -> not (String.IsNullOrWhiteSpace p.baseUrl))
    let customPreset = ProviderPresets.tryFind "custom"
    // 三项显示事实由单一入口 Render 从 VS 写入（事实归模型，桌内不留副本）；
    // 卡片自己的步进机（flow）仍是桌内在途态。
    let mutable selected: ProviderPreset = List.head ProviderPresets.featured
    let mutable switchOffered = false
    let mutable discovered: string list option = None
    let mutable disposed = false

    let effectivePreset () : ProviderPreset =
        match flow.UseAlternate, selected.alternate with
        | true, Some(kind, url) -> { selected with kind = kind; baseUrl = url }
        | _ when selected.key = "custom" -> { selected with label = "自定义 OpenAI 兼容端点"; baseUrl = pane.CustomUrlText }
        | _ -> selected

    let render () =
        let status = flow.Status
        pane.Render
            { Chips =
                [ for p in ProviderPresets.featured -> { Key = p.key; Label = p.label; Selected = (p.key = selected.key) }
                  match customPreset with
                  | Some p -> yield { Key = p.key; Label = "自定义地址"; Selected = (p.key = selected.key) }
                  | None -> () ]
              MoreLabel =
                (if selected.key <> "custom" && ProviderPresets.more |> List.exists (fun p -> p.key = selected.key) then "更多：" + selected.label
                 else "更多")
              Hint = selected.hint
              KeyVisible = selected.needsApiKey
              CustomUrlVisible = (selected.key = "custom")
              Busy = flow.IsBusy
              StatusText = status.text
              StatusIsError = status.isError
              SwitchLabel = (if switchOffered then Some flow.SwitchLabel else None)
              Discovered =
                (match discovered with
                 | Some models -> Some(OnboardingFlow.DiscoveredText(List.length models))
                 | None -> None) }

    let focusFor (preset: ProviderPreset) =
        if preset.key = "custom" then pane.CustomUrlBox.Focus() |> ignore
        elif preset.needsApiKey then pane.KeyBox.Focus() |> ignore

    let applyProbed (id: string) (models: string list) =
        let preset = effectivePreset ()
        let defaultModel = OnboardingRules.pickDefaultModel preset.models models
        flow.BeginApply()
        render ()
        let payload = ProviderPresets.payload id preset.label preset.kind preset.baseUrl None models defaultModel true 120 2
        hooks.UpsertProvider payload (fun ok ->
            flow.Applied(ok, id, defaultModel, preset.label)
            render ())

    let connect () =
        if not flow.IsBusy then
            let preset = effectivePreset ()
            let key = pane.KeyText
            match flow.Check(preset.key = "custom", preset.baseUrl, preset.needsApiKey, key) with
            | OnboardingInput.MissingUrl ->
                render ()
                pane.CustomUrlBox.Focus() |> ignore
            | OnboardingInput.MissingKey ->
                render ()
                pane.KeyBox.Focus() |> ignore
            | OnboardingInput.Ready ->
                let id = ProviderPresets.suggestId preset (hooks.TakenIds())
                flow.Begin id |> ignore
                render ()
                let payload = ProviderPresets.presetPayload preset id (if preset.needsApiKey then Some key else None) []
                hooks.UpsertProvider payload (fun ok ->
                    let probe = flow.Saved(ok, preset.label)
                    render ()
                    if probe then hooks.ProbeProvider id)

    let useLocal () =
        match discovered with
        | Some models when not flow.IsBusy && not models.IsEmpty ->
            let preset = ProviderPresets.tryFind "ollama" |> Option.get
            let id = ProviderPresets.suggestId preset (hooks.TakenIds())
            flow.BeginLocal id |> ignore
            hooks.Report(OnboardingUserMsg.SelectPreset preset.key)
            render ()
            // 默认模型 = 本机已装的第一个：那是用户自己的选择，预设里的名字未必装了
            let defaultModel = List.head models
            let payload = ProviderPresets.payload id preset.label preset.kind preset.baseUrl None models defaultModel true 120 2
            hooks.UpsertProvider payload (fun ok ->
                flow.LocalSaved(ok, id, defaultModel)
                render ())
        | _ -> ()

    do
        pane.ChipClicked.Add(fun key ->
            let pick =
                if Some key = (customPreset |> Option.map (fun p -> p.key)) then customPreset
                else ProviderPresets.featured |> List.tryFind (fun p -> p.key = key)
            match pick with
            | Some preset when flow.Select() ->
                // 事实归模型：卡片只回报选择，宿主投影回来（单一入口 Render）
                hooks.Report(OnboardingUserMsg.SelectPreset preset.key)
                focusFor preset
            | _ -> ())
        pane.MoreClicked.Add(fun anchor ->
            if not flow.IsBusy then
                overlay.ShowMenu(
                    anchor,
                    false,
                    [ for preset in selectable do
                          if not (List.contains preset ProviderPresets.featured) then
                              PopupEntry.create preset.label (fun () ->
                                  flow.Select() |> ignore
                                  hooks.Report(OnboardingUserMsg.SelectPreset preset.key)
                                  if preset.needsApiKey then pane.KeyBox.Focus() |> ignore)
                              |> PopupEntry.markSelected (preset.key = selected.key) ]))
        pane.ConnectClicked.Add connect
        pane.SwitchClicked.Add(fun () ->
            if flow.ToggleAlternate() then
                hooks.Report(OnboardingUserMsg.SwitchOffered false)
                render ())
        pane.AdvancedClicked.Add(fun () -> hooks.OpenAdvanced())
        pane.UseLocalClicked.Add useLocal
        render ()

    member _.View = pane.View
    member _.Pane = pane
    member _.Step = flow.Step
    /// 正在连接中：外壳据此在目录暂时「就绪」时仍保留卡片
    member _.IsBusy = flow.IsBusy
    member _.SelectedPreset = selected
    member _.Discovered = discovered
    member _.SwitchOffered = switchOffered

    /// 单入口幂等渲染：由 SettingsOnboardingVS 驱动
    member this.Render(vs: SettingsOnboardingVS) =
        vs.SelectedPresetKey
        |> Option.iter (fun key ->
            let pick =
                if Some key = (customPreset |> Option.map (fun p -> p.key)) then customPreset
                else ProviderPresets.featured |> List.tryFind (fun p -> p.key = key)
            match pick with
            | Some preset when preset.key <> selected.key ->
                // 换预设时顺带清掉上一次的提示（只在空闲时生效）；同一预设的重复投影不动状态
                flow.Select() |> ignore
                selected <- preset
            | _ -> ())
        discovered <- vs.DiscoveredModels
        switchOffered <- vs.SwitchOffered
        render ()

    /// 探测结果入口。true 表示这是本卡片发起的探测（设置页不应再处理）
    member _.ApplyProbe(providerId: string, ok: bool, models: string list, error: string option) : bool =
        let parsed = if ok then None else Some(ProbeFailureKind.parse (error |> Option.defaultValue "未知原因"))
        match flow.Probed(providerId, ok, (match parsed with Some(_, m) -> m | None -> "")) with
        | OnboardingProbe.Apply id ->
            applyProbed id (if List.isEmpty models then (effectivePreset ()).models else models)
            true
        | OnboardingProbe.Failed id ->
            // 探测失败：撤销刚创建的模型服务，免得留下连不通的半成品
            hooks.DiscardProvider id
            match parsed with
            | Some(kind, _) ->
                hooks.Report(OnboardingUserMsg.SwitchOffered(selected.alternate.IsSome && ProbeFailureKind.suggestsTransportSwitch kind))
            | None -> ()
            render ()
            true
        | OnboardingProbe.NotMine -> false

    /// 刚连接成功且目录里已能用时，取走 (id, 模型) 交给外壳收尾（只取一次）
    member _.TakeConnected(catalog: Catalog) : (string * string) option =
        match flow.Connected with
        | Some(id, _) ->
            match Catalog.tryProvider id catalog with
            | Some p when p.enabled && not (List.isEmpty p.models) -> flow.TakeConnected()
            | _ -> None
        | None -> None

    /// 卡片显示时：发现本机 Ollama（每次显示只探一次）
    member this.StartDiscovery() =
        if flow.StartDiscovery() then
            hooks.DiscoverLocal().ContinueWith((fun (t: Task<string list option>) ->
                let result = if t.IsCompletedSuccessfully then t.Result else None
                // 结果回 UI 线程再回报（事实进模型，宿主投影回来）：探测在后台线程完成
                if not disposed then
                    Dispatcher.UIThread.Post(fun () -> if not disposed then this.ShowDiscovered result)),
                TaskScheduler.Default)
            |> ignore

    member _.ApplyDiscovered(result: string list option) =
        // 发现的清单归模型（宿主投影回来）；卡片这里只回答「要不要显示一键使用」
        flow.ShowDiscovered result |> ignore
        hooks.Report(OnboardingUserMsg.Discovered result)
        render ()

    member this.ShowDiscovered(result: string list option) = this.ApplyDiscovered result

    /// 卡片收起（有可用模型）后复位，下次再显示是干净的
    member _.Reset() =
        if flow.Reset() then
            pane.ClearKey()
            render ()

    member _.FocusInitial() = pane.FocusInitial selected.needsApiKey
    member _.Dispose() = disposed <- true
