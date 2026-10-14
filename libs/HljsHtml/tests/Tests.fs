module Tests

open Xunit
open Wanxiang.UI

// 以下迁自旧 PolishTests（Highlight. 改为 HljsHtml.）。
let private textOf (html: string) =
    HljsHtml.parseHtml html |> List.map (fun t -> t.text) |> String.concat ""

let private kindOfFirst (html: string) (text: string) =
    HljsHtml.parseHtml html |> List.tryFind (fun t -> t.text = text) |> Option.map (fun t -> t.kind)

[<Fact>]
let ``span parser restores the source text exactly`` () =
    let html =
        "<span class=\"hljs-keyword\">let</span> <span class=\"hljs-keyword\">rec</span> fib n "
        + "<span class=\"hljs-operator\">=</span>\n  <span class=\"hljs-number\">0</span>"
    Assert.Equal("let rec fib n =\n  0", textOf html)

[<Fact>]
let ``html entities are decoded back to source characters`` () =
    // hljs 会把 -> 转义成 -&gt;；不解码的话代码块里会显示实体本身
    let html = "<span class=\"hljs-operator\">-&gt;</span> &amp; &lt;T&gt; &quot;q&quot;"
    Assert.Equal("-> & <T> \"q\"", textOf html)

[<Fact>]
let ``the innermost scope wins over the enclosing one`` () =
    // 字符串里的插值该按变量着色，否则整个模板串糊成一块
    let html =
        "<span class=\"hljs-string\">`a</span><span class=\"hljs-subst\">${x}</span>"
        + "<span class=\"hljs-string\">b`</span>"
    Assert.Equal(Some CodeString, kindOfFirst html "`a")
    Assert.Equal(Some CodeVariable, kindOfFirst html "${x}")

[<Fact>]
let ``unknown scopes inherit the enclosing kind instead of resetting to plain`` () =
    let html = "<span class=\"hljs-comment\">// <span class=\"hljs-unheard-of\">x</span></span>"
    // 继承成功的证据正是它与外层合并成了一段注释
    Assert.Equal(Some CodeComment, kindOfFirst html "// x")

[<Fact>]
let ``hljs sub scopes map through their dotted path`` () =
    Assert.Equal(Some CodeFunction, HljsHtml.kindOfScope "title.function")
    Assert.Equal(Some CodeType, HljsHtml.kindOfScope "title.class")
    Assert.Equal(Some CodeString, HljsHtml.kindOfScope "char.escape")
    Assert.Equal(None, HljsHtml.kindOfScope "emphasis")

[<Fact>]
let ``a deep unregistered sub scope falls back to its first segment`` () =
    // hljs 的 class 写法是 `hljs-title function_ invoke__`
    let html = "<span class=\"hljs-title function_ invoke__\">f</span>"
    Assert.Equal(Some CodeFunction, kindOfFirst html "f")

[<Fact>]
let ``adjacent tokens of one kind are merged`` () =
    let html = "<span class=\"hljs-keyword\">le</span><span class=\"hljs-keyword\">t</span>"
    Assert.Single(HljsHtml.parseHtml html) |> ignore

[<Fact>]
let ``malformed markup never throws and never drops visible text`` () =
    for html in [ "<span class=\"hljs-keyword\">let"; "</span></span>x"; "<span"; ""; "<>"; "&notanentity;" ] do
        let rebuilt = textOf html
        Assert.NotNull rebuilt
    Assert.Equal("let", textOf "<span class=\"hljs-keyword\">let")
    Assert.Equal("x", textOf "</span></span>x")


// 本库补充：边界与失败路径。

[<Fact>]
let ``空串得到空列表`` () = Assert.Empty(HljsHtml.parseHtml "")

[<Fact>]
let ``纯文本整段为 CodePlain`` () =
    Assert.Equal<CodeToken list>([ { text = "a < b"; kind = CodePlain } ], HljsHtml.parseHtml "a &lt; b")

[<Fact>]
let ``多余的闭合标签不弹出外层之外的类别`` () =
    let t = HljsHtml.parseHtml "</span><span class=\"hljs-number\">1</span></span>2"
    Assert.Equal<CodeToken list>([ { text = "1"; kind = CodeNumber }; { text = "2"; kind = CodePlain } ], t)

[<Fact>]
let ``没有 class 的 span 继承外层`` () =
    let t = HljsHtml.parseHtml "<span class=\"hljs-string\">a<span>b</span></span>"
    Assert.Equal<CodeToken list>([ { text = "ab"; kind = CodeString } ], t)

[<Fact>]
let ``class 引号没闭合时按无类别处理`` () =
    let t = HljsHtml.parseHtml "<span class=\"hljs-keyword>x</span>"
    Assert.Equal<CodeToken list>([ { text = "x"; kind = CodePlain } ], t)

[<Fact>]
let ``每个登记的 scope 都映射到一个类别`` () =
    for s in [ "keyword"; "built_in"; "regexp"; "doctag"; "number"; "section"; "meta.prompt"; "template-variable"; "punctuation"; "addition"; "deletion" ] do
        Assert.True((HljsHtml.kindOfScope s).IsSome, s)
