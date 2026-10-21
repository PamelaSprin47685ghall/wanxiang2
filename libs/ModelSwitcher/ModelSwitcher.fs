namespace Wanxiang.UI

open System

/// 快速切换模型里的一行（决策 240）。
type ModelOption =
    {
        providerId: string
        providerLabel: string
        model: string
        /// 本对话（或新对话草稿）当前使用的就是它
        isCurrent: bool
        /// 新对话的默认模型（与 `Catalog.defaultSelection` 同一规则）
        isDefault: bool
    }

/// 快速切换模型的纯逻辑：搜索、排序、文案。从目录列出选项在组合者 Catalog。
module ModelSwitcher =

    /// 搜索：按空白拆词，每个词都要出现在「模型名」或「服务名」里（不区分大小写）。
    /// 空查询返回全部。命中模型名开头的排前面，其余保持原顺序。
    let filter (query: string) (items: ModelOption list) : ModelOption list =
        let terms =
            if String.IsNullOrWhiteSpace query then
                [||]
            else
                query.Trim().ToLowerInvariant().Split([| ' '; '\t'; '　' |], StringSplitOptions.RemoveEmptyEntries)

        if terms.Length = 0 then
            items
        else
            let hay (o: ModelOption) =
                (o.model + " " + o.providerLabel + " " + o.providerId).ToLowerInvariant()

            let matched =
                items
                |> List.filter (fun o -> let h = hay o in terms |> Array.forall h.Contains)

            let first = terms[0]

            let prefix, rest =
                matched |> List.partition (fun o -> o.model.ToLowerInvariant().StartsWith first)

            prefix @ rest

    /// 切换后的确认文案：明说「从下一条回复开始」，已有回复不受影响。
    let switchedToast (providerLabel: string) (model: string) : string =
        sprintf "已切换到 %s · %s，从下一条回复开始生效" providerLabel model

    /// 浮层底部常驻说明。
    let appliesNote = "切换只影响之后的回复，已有回复不会改变。"
