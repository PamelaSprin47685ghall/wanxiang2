namespace Wanxiang.UI

// 照抄旧 Views/Sidebar.fs searchDebounce.Tick 的判定
open System

module SearchRequery =
    let query (text: string) = (if isNull text then "" else text).Trim()
    let shouldQuery (text: string) (searchHitsQuery: string) =
        let q = (if isNull text then "" else text).Trim()
        if not (q <> "" && q = searchHitsQuery) then true else false
