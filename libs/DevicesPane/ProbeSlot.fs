namespace Wanxiang.UI

open System.Collections.Generic

/// 模型服务探活的归属与结果缓存（迁自旧 Views/SettingsProviders.fs 的 activeProbe、probeResults）。
/// 编辑器打开时登记「这次探活归我」；结果到达时只有同一模型服务的结果才交给编辑器。
/// 编辑器的控件回调仍归宿主。
type ProbeSlot() =
    let mutable active: string option = None
    let results = Dictionary<string, string list>()

    /// 编辑器为这个模型服务发起探活。
    member _.Open(providerId: string) = active <- Some providerId

    /// 编辑器关闭：不再有归属。
    member _.Close() = active <- None

    member _.Active = active

    /// 结果到达：属于当前编辑器则清掉归属并返回 true；否则返回 false（不动归属）。
    member _.Claim(providerId: string) : bool =
        match active with
        | Some id when id = providerId ->
            active <- None
            true
        | _ -> false

    /// 记下探活得到的模型清单（是否记由宿主按反馈决定）。
    member _.Remember(providerId: string, models: string list) = results[providerId] <- models

    /// 编辑器打开时用的模型清单：有非空缓存用缓存，否则用配置里的。
    member _.EffectiveModels(providerId: string, configured: string list) : string list =
        match results.TryGetValue providerId with
        | true, cached when not (List.isEmpty cached) -> cached
        | _ -> configured

    /// 默认模型：配置的默认在清单里就用它，否则清单第一项（旧编辑器回填规则）。
    static member DefaultModel(configuredDefault: string, models: string list) : string =
        if List.contains configuredDefault models then
            configuredDefault
        else
            models |> List.tryHead |> Option.defaultValue ""
