namespace Wanxiang.UI

open System
open System.Collections.Generic
open System.Net
open System.Text

/// 代码片段的着色类别。
///
/// 这一层刻意比 hljs 的 scope 粗：hljs 有近五十种 scope，全部各给一色
/// 会让代码块变成调色板。这里收敛到十二类，映射表在 `HljsHtml.kindOfScope`。
type CodeTokenKind =
    | CodePlain
    | CodeKeyword
    | CodeString
    | CodeComment
    | CodeNumber
    | CodeType
    | CodeFunction
    | CodeMeta
    | CodeVariable
    | CodeOperator
    | CodeAddition
    | CodeDeletion

type CodeToken = { text: string; kind: CodeTokenKind }

/// 把 highlight.js 的 HTML 输出翻成一串带类别的文本片段（从旧 Rich/Highlight.fs 拆出纯逻辑）。
///
/// 语法本身完全交给 hljs，这里只做两件事：解析它吐出的 span 嵌套，以及把 scope 收敛到本项目的配色类别。
/// 后台排队、缓存与重画通知属于界面，留在调用方。
module HljsHtml =

    /// hljs scope → 本项目类别。
    ///
    /// hljs 用 `class="hljs-title function_"` 表达 `title.function` 这种子 scope，
    /// 所以要先还原成点号路径，再按「先精确、后退到首段」查表。
    let kindOfScope (scope: string) : CodeTokenKind option =
        match scope with
        | "keyword" | "literal" | "template-tag" -> Some CodeKeyword
        | "type" | "built_in" | "class" | "tag" | "name" | "selector-tag"
        | "title.class" | "title.class.inherited" -> Some CodeType
        | "string" | "regexp" | "char" | "char.escape" | "formula" | "meta.string" -> Some CodeString
        | "comment" | "quote" | "doctag" -> Some CodeComment
        | "number" -> Some CodeNumber
        | "title" | "title.function" | "title.function.invoke" | "section" -> Some CodeFunction
        | "meta" | "meta.prompt" | "meta.keyword" | "link" | "code" -> Some CodeMeta
        | "variable" | "variable.language" | "variable.constant" | "attr" | "attribute"
        | "property" | "symbol" | "params" | "subst" | "template-variable" | "bullet"
        | "selector-id" | "selector-class" | "selector-attr" | "selector-pseudo" -> Some CodeVariable
        | "operator" | "punctuation" -> Some CodeOperator
        | "addition" -> Some CodeAddition
        | "deletion" -> Some CodeDeletion
        | _ -> None

    /// `hljs-title function_` → `title.function`
    let private scopeOfClassAttribute (classes: string) =
        classes.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun cls ->
            let trimmed = if cls.StartsWith "hljs-" then cls.Substring 5 else cls
            trimmed.TrimEnd '_')
        |> Array.filter (fun s -> s.Length > 0)
        |> String.concat "."

    let private kindOfClassAttribute (classes: string) =
        let scope = scopeOfClassAttribute classes
        match kindOfScope scope with
        | Some kind -> Some kind
        // `title.function.invoke` 这类未登记的深层子 scope 退到首段（`title`）
        | None ->
            match scope.IndexOf '.' with
            | -1 -> None
            | cut -> kindOfScope (scope.Substring(0, cut))

    /// 解析 hljs 的 span 嵌套。内层 scope 优先：`string` 里嵌 `subst` 时，
    /// 插值部分该按变量着色，而不是继续当字符串。
    let parseHtml (html: string) : CodeToken list =
        let tokens = ResizeArray<CodeToken>()
        let buffer = StringBuilder()
        let stack = ResizeArray<CodeTokenKind>()
        let currentKind () = if stack.Count = 0 then CodePlain else stack[stack.Count - 1]

        let flush () =
            if buffer.Length > 0 then
                tokens.Add { text = WebUtility.HtmlDecode(buffer.ToString()); kind = currentKind () }
                buffer.Clear() |> ignore

        let mutable i = 0
        while i < html.Length do
            if html[i] = '<' then
                let close = html.IndexOf('>', i)
                if close < 0 then
                    // 标签没闭合：剩下的当文本，绝不丢内容
                    buffer.Append(html.Substring i) |> ignore
                    i <- html.Length
                else
                    let tag = html.Substring(i + 1, close - i - 1)
                    flush ()
                    if tag.StartsWith "/" then
                        if stack.Count > 0 then stack.RemoveAt(stack.Count - 1)
                    else
                        let kind =
                            let marker = "class=\""
                            match tag.IndexOf marker with
                            | -1 -> None
                            | at ->
                                let from = at + marker.Length
                                match tag.IndexOf('"', from) with
                                | -1 -> None
                                | until -> kindOfClassAttribute (tag.Substring(from, until - from))
                        // 未识别的 scope 继承外层，而不是打回纯文本
                        stack.Add(defaultArg kind (currentKind ()))
                    i <- close + 1
            else
                let next = html.IndexOf('<', i)
                let until = if next < 0 then html.Length else next
                buffer.Append(html, i, until - i) |> ignore
                i <- until
        flush ()

        // 合并同类相邻片段：每个 Run 都是一次文本整形，能省则省
        let merged = ResizeArray<CodeToken>()
        for token in tokens do
            if token.text.Length > 0 then
                if merged.Count > 0 && merged[merged.Count - 1].kind = token.kind then
                    let last = merged[merged.Count - 1]
                    merged[merged.Count - 1] <- { last with text = last.text + token.text }
                else
                    merged.Add token
        List.ofSeq merged
