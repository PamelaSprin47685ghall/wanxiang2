namespace Wanxiang2.CodeTint

open System
open Wanxiang.UI

/// 宿主给的着色环境。脚本后端（RichBackend）与线程、计时器都留在宿主：
/// highlightHtml 是后端的「代码进、hljs HTML 出」；background 把一段工作放到后台；
/// after 在若干毫秒后执行一次；synchronous 为真时（浏览器端宿主 JS 引擎够快且没有后台线程）当场算。
type CodeTintHost =
    { HighlightHtml: string * string -> string option
      Background: (unit -> unit) -> unit
      After: int -> (unit -> unit) -> unit
      Synchronous: bool }

/// 代码着色（旧 Rich/Highlight.fs，模块状态改为实例）：语言解析、记忆化、后台排队与合并重画通知。
/// 不装后端（highlightHtml 总给 None）时整段纯文本，功能缺失但绝不崩。
type CodeTint(host: CodeTintHost) =
    let cache = BoundedCache<struct (string * string), CodeToken list>(192)
    let queue = HighlightQueue<struct (string * string)>()
    let ready = Event<unit>()

    let plainOf (code: string) = [ { text = code; kind = CodePlain } ]

    let compute (canonical: string) (code: string) =
        match host.HighlightHtml(code, canonical) with
        | Some html ->
            match HljsHtml.parseHtml html with
            | [] -> plainOf code
            | tokens -> tokens
        | None -> plainOf code

    /// 窗口内第一个完成负责安排通知，其余搭车：二十个代码块一起完成只重画一次。
    let scheduleNotify () =
        host.After HighlightQueue<struct (string * string)>.NotifyWindowMs (fun () ->
            queue.NotifyFired()
            ready.Trigger())

    let enqueue (key: struct (string * string)) =
        if queue.TryClaim key then
            let struct (canonical, code) = key
            host.Background(fun () ->
                let mutable ok = false
                try
                    cache.Set(key, compute canonical code)
                    ok <- true
                finally
                    if queue.Finished(key, ok) then scheduleNotify ())

    /// 后台算完一段着色时触发（在宿主 After 回调的线程上），视图据此重画一次。
    [<CLIEvent>]
    member _.Ready = ready.Publish

    /// 着色。allowHighlight = false（流式进行中）直接给纯文本；语言认不出、后端失败同样纯文本。
    /// 缓存未命中时先给纯文本，算好后触发 Ready。
    member _.Tokenize(allowHighlight: bool, language: string, code: string) : CodeToken list =
        if String.IsNullOrEmpty code then []
        else
            match RichAssets.resolveLanguage language with
            | Some canonical when allowHighlight ->
                let key = struct (canonical, code)
                match cache.TryGet key with
                | Some hit -> hit
                | None when host.Synchronous ->
                    let tokens = compute canonical code
                    cache.Set(key, tokens)
                    tokens
                | None ->
                    enqueue key
                    plainOf code
            | _ -> plainOf code
