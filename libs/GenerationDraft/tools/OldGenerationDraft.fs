namespace Wanxiang.UI

type GenerationSave<'T> = { revision: int; epoch: int; submitted: 'T }

/// 旧 SettingsGeneral 的草稿字段与分支照抄：generationStatus.Text 换成 status，
/// setGenerationPending 只保留它对 generationSaving 的作用，SetCatalog 的回填换成 fill
type GenerationDraft<'T when 'T: equality>(initial: 'T) =
    let mutable generationSaving = false
    let mutable generationDirty = false
    let mutable generationSyncing = false
    let mutable generationRevision = 0
    let mutable generationBaseline = initial
    let mutable generationScope = ""
    let mutable draftScope = ""
    let mutable scopeEpoch = 0
    let mutable status = "修改后保存，仅用于新对话。"
    let setGenerationPending pending = generationSaving <- pending

    member _.Status = status
    member _.Dirty = generationDirty
    member _.Saving = generationSaving
    member _.Baseline = generationBaseline

    member _.Edited() =
        if not generationSyncing then
            if not generationDirty then draftScope <- generationScope
            generationRevision <- generationRevision + 1
            generationDirty <- true
            status <-
                if draftScope <> generationScope then "主机已切换。旧草稿仍在，但不能保存到另一台主机。请返回原主机，或放弃修改。"
                else "有未保存的修改。离开设置后，草稿仍留在当前窗口。"

    member _.CatalogArrived(next: 'T, fill: unit -> unit) =
        if not generationDirty && not generationSaving then
            generationSyncing <- true
            try
                fill ()
                generationBaseline <- next
            finally
                generationSyncing <- false
        elif not generationSaving && draftScope = generationScope && next <> generationBaseline then
            status <- "主机默认值已变化。草稿已保留；请核对后保存，或放弃修改。"

    member _.SetScope(instance: string) =
        if generationScope <> instance then
            scopeEpoch <- scopeEpoch + 1
            generationScope <- instance
            generationSaving <- false
            setGenerationPending false
            if generationDirty then
                status <-
                    if draftScope = instance then "已回到草稿所属的主机。修改尚未保存。"
                    else "主机已切换。旧草稿仍在，但不能保存到另一台主机。请返回原主机，或放弃修改。"

    member this.Discard(current: 'T, fill: unit -> unit) : bool =
        if not generationSaving then
            generationDirty <- false
            draftScope <- generationScope
            generationRevision <- generationRevision + 1
            this.CatalogArrived(current, fill)
            status <- "已恢复当前主机的默认值。"
            setGenerationPending false
            true
        else false

    member _.CanSave = not (generationSaving || (generationDirty && draftScope <> generationScope))

    member _.BeginSave(submitted: 'T) : GenerationSave<'T> =
        setGenerationPending true
        status <- "正在保存到主机…"
        { revision = generationRevision; epoch = scopeEpoch; submitted = submitted }

    member _.Finish(save: GenerationSave<'T>, ok: bool) : bool =
        if save.epoch <> scopeEpoch then false
        elif ok then
            setGenerationPending false
            generationBaseline <- save.submitted
            generationDirty <- generationRevision <> save.revision
            status <-
                if generationDirty then "上一版已保存，后续修改尚未保存。"
                else "已保存。之后新建的对话将使用这些默认值。"
            false
        else
            setGenerationPending false
            generationDirty <- true
            status <- "保存失败。修改仍在当前窗口，可以重试。"
            true
