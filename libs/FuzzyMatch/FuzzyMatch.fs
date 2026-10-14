namespace Wanxiang.UI

open System

/// 子序列模糊匹配打分（从旧 CommandPalette 拆出）。
module FuzzyMatch =

    /// 子序列模糊匹配打分；不匹配返回 None。连续、词首、开头命中加分，跨度越大越低。
    let score (query: string) (text: string) : int option =
        let q = query.Trim().ToLowerInvariant()
        let t = text.ToLowerInvariant()
        if q = "" then Some 0
        elif t.Contains q then
            Some(1000 - (t.IndexOf q) * 5 + (if t.StartsWith q then 200 else 0) - t.Length)
        else
            let mutable ti = 0
            let mutable score = 0
            let mutable last = -2
            let mutable ok = true
            let mutable qi = 0
            while ok && qi < q.Length do
                let ch = q[qi]
                if ch = ' ' then qi <- qi + 1
                else
                    let idx = t.IndexOf(ch, ti)
                    if idx < 0 then ok <- false
                    else
                        score <- score + (if idx = last + 1 then 15 else 1)
                        if idx = 0 || not (Char.IsLetterOrDigit t[idx - 1]) then score <- score + 10
                        last <- idx
                        ti <- idx + 1
                        qi <- qi + 1
            if ok then Some(score * 10 - t.Length) else None

