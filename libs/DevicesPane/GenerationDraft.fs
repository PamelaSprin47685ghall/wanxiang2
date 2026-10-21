namespace Wanxiang.UI

/// 一次在途保存：提交时的修订号、主机纪元与提交的值。
type GenerationSave<'T> = { revision: int; epoch: int; submitted: 'T }

/// 设置页「生成」草稿的状态机（旧 Views/SettingsGeneral.fs 的 generationDirty/Saving/Syncing/Revision/
/// Baseline/Scope、draftScope、scopeEpoch 与状态文案）。
///
/// 只保留一份可见草稿；草稿属于开始编辑时的主机，切到别的主机后不能保存，回到原主机才能保存。
/// 目录更新不覆盖未保存的草稿；往返途中的编辑不会被较早的保存确认标成已保存。
/// 表单控件、校验与发送由调用方负责：需要回填表单时，本对象调用传入的 fill，期间的编辑不算修改。
type GenerationDraft<'T when 'T: equality>(initial: 'T) =
    let mutable dirty = false
    let mutable saving = false
    let mutable syncing = false
    let mutable revision = 0
    let mutable baseline = initial
    let mutable scope = ""
    let mutable draftScope = ""
    let mutable epoch = 0
    let mutable status = "修改后保存，仅用于新对话。"

    let switchedAway = "主机已切换。旧草稿仍在，但不能保存到另一台主机。请返回原主机，或放弃修改。"

    member _.Status = status
    member _.Dirty = dirty
    member _.Saving = saving
    member _.Baseline = baseline

    /// 表单任一字段被改。回填期间的改动不算。
    member _.Edited() =
        if not syncing then
            if not dirty then draftScope <- scope
            revision <- revision + 1
            dirty <- true
            status <- if draftScope <> scope then switchedAway else "有未保存的修改。离开设置后，草稿仍留在当前窗口。"

    /// 主机目录到达。没有草稿且不在保存时回填表单并以它为基线；
    /// 有草稿且主机默认值变了时只提示。
    member _.CatalogArrived(next: 'T, fill: unit -> unit) =
        if not dirty && not saving then
            syncing <- true
            try
                fill ()
                baseline <- next
            finally
                syncing <- false
        elif not saving && draftScope = scope && next <> baseline then
            status <- "主机默认值已变化。草稿已保留；请核对后保存，或放弃修改。"

    /// 切换目标主机：作废在途保存的确认。
    member _.ApplyScope(instance: string) =
        if scope <> instance then
            epoch <- epoch + 1
            scope <- instance
            saving <- false
            if dirty then
                status <- if draftScope = instance then "已回到草稿所属的主机。修改尚未保存。" else switchedAway

    /// 放弃修改并按 current 回填。保存途中不做，返回 false。
    member this.Discard(current: 'T, fill: unit -> unit) : bool =
        if saving then false
        else
            dirty <- false
            draftScope <- scope
            revision <- revision + 1
            this.CatalogArrived(current, fill)
            status <- "已恢复当前主机的默认值。"
            saving <- false
            true

    /// 不在保存，且草稿属于当前主机（或没有草稿）时才能保存。
    member _.CanSave = not (saving || (dirty && draftScope <> scope))

    /// 校验通过后开始保存。
    member _.BeginSave(submitted: 'T) : GenerationSave<'T> =
        saving <- true
        status <- "正在保存到主机…"
        { revision = revision; epoch = epoch; submitted = submitted }

    /// 主机回复。切换过主机的旧确认被忽略。返回 true 表示保存失败、调用方应弹「保存失败，请重试。」。
    member _.Finish(save: GenerationSave<'T>, ok: bool) : bool =
        if save.epoch <> epoch then false
        elif ok then
            saving <- false
            baseline <- save.submitted
            dirty <- revision <> save.revision
            status <- if dirty then "上一版已保存，后续修改尚未保存。" else "已保存。之后新建的对话将使用这些默认值。"
            false
        else
            saving <- false
            dirty <- true
            status <- "保存失败。修改仍在当前窗口，可以重试。"
            true
