namespace Wanxiang.UI

open System

/// 旧 Views/Sidebar.fs 搜索防抖到点时的判定：词没变（已为它发过查询，结果已到或在途）就不重发，
/// 否则迟到的 Tick 会清空刚到的命中。计时器仍归 Sidebar
module SearchRequery =

    /// 输入框文本归一（空、去首尾空白）
    let query (text: string) = (if isNull text then "" else text).Trim()

    /// 到点时是否发起正文搜索；lastQuery 为上次已发出查询的词
    let shouldQuery (text: string) (lastQuery: string) =
        let q = query text
        not (q <> "" && q = lastQuery)
