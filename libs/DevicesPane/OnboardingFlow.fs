namespace Wanxiang.UI

/// 首次运行卡片所处阶段。
[<RequireQualifiedAccess>]
type OnboardingStep =
    | Idle
    /// 正在保存模型服务（第一次 upsert）
    | Saving
    /// 已保存，正在向对方请求模型清单
    | Probing
    /// 已拿到清单，正在把清单与默认模型写回
    | Applying

/// 点「连接」前的输入检查结果。
[<RequireQualifiedAccess>]
type OnboardingInput =
    | Ready
    /// 自定义地址没填（宿主聚焦地址框）
    | MissingUrl
    /// 密钥没填（宿主聚焦密钥框）
    | MissingKey

/// 探测结果对本卡片意味着什么。
[<RequireQualifiedAccess>]
type OnboardingProbe =
    /// 不是本卡片发起的探测：交给设置页
    | NotMine
    /// 探测成功：宿主选定默认模型后调用 BeginApply / Applied
    | Apply of providerId: string
    /// 探测失败：卡片已回到空闲并给出说明；宿主撤销这个刚创建的模型服务
    | Failed of providerId: string

/// 卡片底部的状态文字。
type OnboardingStatus = { text: string; isError: bool }

/// 首次运行「添加一个模型服务」卡片的步骤机（迁自旧 Views/OnboardingCard.fs 的 step、attemptId、connected、
/// useAlternate、discoveryStarted 及其转移）。保存、探测、发现本机 Ollama 的调用和控件都归宿主；
/// 发现的模型清单归模型（引导卡事实），本对象不存副本。
/// 本对象只回答「现在在哪一步、下一步是什么、该显示什么」。不认识预设类型：宿主给出 id、名称与检查所需的事实。
type OnboardingFlow() =
    let mutable step = OnboardingStep.Idle
    let mutable attempt: string option = None
    let mutable connected: (string * string) option = None
    let mutable useAlternate = false
    let mutable discoveryStarted = false
    let mutable status = { text = ""; isError = false }

    let setStatus text isError =
        status <- { text = text; isError = isError }

    let fail message =
        step <- OnboardingStep.Idle
        attempt <- None
        setStatus message true

    member _.Step = step
    /// 正在连接中：外壳据此在目录暂时「就绪」时仍保留卡片。
    member _.IsBusy = step <> OnboardingStep.Idle
    member _.Attempt = attempt
    member _.UseAlternate = useAlternate
    member _.Status = status

    /// 选了另一家（只在空闲时生效）：回到原生传输，清掉状态。
    member _.Select() : bool =
        if step = OnboardingStep.Idle then
            useAlternate <- false
            setStatus "" false
            true
        else
            false

    /// 「改用兼容端点 / 改回原生接口」（只在空闲时生效）。
    member _.ToggleAlternate() : bool =
        if step = OnboardingStep.Idle then
            useAlternate <- not useAlternate
            setStatus "已切换传输，请再点「连接」重试。" false
            true
        else
            false

    /// 切换按钮上的文字。
    member _.SwitchLabel = if useAlternate then "改回原生接口" else "改用兼容端点"

    /// 点「连接」时的输入检查；缺项时给出说明。
    member _.Check(isCustom: bool, baseUrl: string, needsKey: bool, key: string) : OnboardingInput =
        if isCustom && System.String.IsNullOrWhiteSpace baseUrl then
            setStatus "还没有填写服务地址。请先填入兼容 OpenAI 的接口地址，再点「连接」。" true
            OnboardingInput.MissingUrl
        elif needsKey && System.String.IsNullOrWhiteSpace key then
            setStatus "还没有填写密钥。请先粘贴 API Key，再点「连接」。" true
            OnboardingInput.MissingKey
        else
            OnboardingInput.Ready

    /// 开始保存这个模型服务。只在空闲时生效。
    member _.Begin(providerId: string) : bool =
        if step = OnboardingStep.Idle then
            step <- OnboardingStep.Saving
            attempt <- Some providerId
            setStatus "正在保存模型服务…" false
            true
        else
            false

    /// 第一次保存的结果。成功进入探测（宿主随后发起探测）并返回 true。label：实际使用的模型服务名称。
    member _.Saved(ok: bool, label: string) : bool =
        if step <> OnboardingStep.Saving then
            false
        elif ok then
            step <- OnboardingStep.Probing
            setStatus (sprintf "正在向 %s 获取模型列表…" label) false
            true
        else
            fail "保存失败：配置没有写入。请检查密钥后重试，或到设置里查看详情。"
            false

    /// 探测结果入口。failureMessage：失败时给用户看的说明（宿主由主机下发的原因解析出来）。
    member _.Probed(providerId: string, ok: bool, failureMessage: string) : OnboardingProbe =
        match step, attempt with
        | OnboardingStep.Probing, Some id when id = providerId ->
            if ok then
                OnboardingProbe.Apply id
            else
                fail failureMessage
                OnboardingProbe.Failed id
        | _ -> OnboardingProbe.NotMine

    /// 第二次写回开始（清单与默认模型）。
    member _.BeginApply() =
        step <- OnboardingStep.Applying
        setStatus "已获取模型列表，正在选定默认模型…" false

    /// 第二次写回的结果。
    member _.Applied(ok: bool, providerId: string, defaultModel: string, label: string) =
        if ok then
            connected <- Some(providerId, defaultModel)
            step <- OnboardingStep.Idle
            attempt <- None
            setStatus (sprintf "已连接 %s，默认模型 %s。" label defaultModel) false
        else
            fail "保存模型列表失败：配置没有写入。请重试一次，仍不行就到设置里添加。"

    /// 一键使用本机 Ollama：开始保存。只在空闲时生效。
    member _.BeginLocal(providerId: string) : bool =
        if step = OnboardingStep.Idle then
            useAlternate <- false
            attempt <- Some providerId
            step <- OnboardingStep.Saving
            setStatus "正在保存本机 Ollama…" false
            true
        else
            false

    /// 本机 Ollama 保存的结果（一次写入就带清单与默认模型，不再探测）。
    member _.LocalSaved(ok: bool, providerId: string, defaultModel: string) =
        if ok then
            connected <- Some(providerId, defaultModel)
            step <- OnboardingStep.Idle
            attempt <- None
            setStatus (sprintf "已连接本机 Ollama，默认模型 %s。" defaultModel) false
        else
            fail "保存失败：配置没有写入。请重试，或到设置里添加。"

    /// 连接成功、等外壳收尾的 (id, 默认模型)。
    member _.Connected = connected

    /// 外壳确认目录里这个模型服务已可用后取走（只取一次）。
    member _.TakeConnected() : (string * string) option =
        let c = connected
        connected <- None
        c

    /// 卡片显示时发现本机 Ollama：每次显示只探一次，返回是否应发起。
    member _.StartDiscovery() : bool =
        if discoveryStarted then
            false
        else
            discoveryStarted <- true
            true

    /// 发现结果到达。返回是否应显示「一键使用」（有至少一个模型）。
    member _.ShowDiscovered(result: string list option) : bool =
        match result with
        | Some models -> not (List.isEmpty models)
        | None -> false

    /// 发现面板上的说明。
    static member DiscoveredText(count: int) =
        sprintf "发现本机 Ollama 正在运行，有 %d 个模型。" count

    /// 卡片收起后复位（只在空闲时）；返回 true 时宿主清空密钥框。
    member _.Reset() : bool =
        if step = OnboardingStep.Idle then
            setStatus "" false
            useAlternate <- false
            attempt <- None
            discoveryStarted <- false
            true
        else
            false
