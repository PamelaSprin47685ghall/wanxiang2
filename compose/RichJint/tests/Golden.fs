module Golden
open System.Text

/// 金样正文：每段输入的语言解析、hljs HTML、解析后的 token、KaTeX HTML、KNode 树。
let render () : string =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    for (lang, code) in Scenario.code do
        line (sprintf "== code %s" (System.Text.Json.JsonSerializer.Serialize lang))
        match Api.resolve lang with
        | None -> line "resolve: none"
        | Some c ->
            line (sprintf "resolve: %s requiresLoad: %b" c (Api.requires c))
            match Api.highlight code c with
            | None -> line "html: none"
            | Some html ->
                line ("html: " + html)
                for t in Api.tokens html do line ("token: " + t)
    for (tex, disp) in Scenario.math do
        line (sprintf "== math %s display=%b" (System.Text.Json.JsonSerializer.Serialize tex) disp)
        match Api.math tex disp with
        | None -> line "html: none"
        | Some html ->
            line ("html: " + html)
            line (sprintf "visual: %b display: %b" (Api.visual html) (Api.display html))
            line ("tree: " + Api.katex html)
    sb.ToString()
