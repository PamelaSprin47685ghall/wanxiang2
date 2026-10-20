namespace Wanxiang.UI

open System.Collections.Generic

/// 分区的构建缓存与滚动位置（迁自旧 Views/SettingsView.fs 的 builtSections、scrollPositions、current）。
/// 每个分区只构建一次：面板内部复用同一批控件，重复构建会把它们挂到第二个父级上。
type SectionMemory<'section, 'built when 'section: equality>(initial: 'section) =
    let built = Dictionary<'section, 'built>(HashIdentity.Structural)
    let offsets = Dictionary<'section, float>(HashIdentity.Structural)
    let mutable current = initial

    member _.Current = current

    member _.TryBuilt(section: 'section) : 'built option =
        match built.TryGetValue section with
        | true, b -> Some b
        | _ -> None

    member _.Keep(section: 'section, value: 'built) = built[section] <- value

    /// 数据变化后需要重建的分区：丢弃缓存。
    member _.Invalidate(sections: 'section list) =
        for s in sections do built.Remove s |> ignore

    /// 切换分区。currentOffset 是离开时当前分区的滚动位置（没有滚动容器时为 None）。
    /// 分区变了才记下旧分区位置（位置大于 0，或该分区还没记过），并返回新分区应恢复的位置；没变返回 None。
    member _.Select(section: 'section, currentOffset: float option) : float option =
        let changed = current <> section
        if changed then
            currentOffset |> Option.iter (fun y ->
                if y > 0.0 || not (offsets.ContainsKey current) then offsets[current] <- y)
        current <- section
        if changed then
            Some(match offsets.TryGetValue section with
                 | true, y -> y
                 | _ -> 0.0)
        else None

    member _.Offset(section: 'section) : float =
        match offsets.TryGetValue section with
        | true, y -> y
        | _ -> 0.0

    member _.ApplyOffset(section: 'section, y: float) = offsets[section] <- y
