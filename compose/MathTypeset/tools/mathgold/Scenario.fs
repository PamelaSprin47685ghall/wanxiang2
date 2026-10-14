module Scenario

/// 金样输入：着色（语言标注, 代码）与排版（TeX, 是否展示式）。两边（旧代码生成器、新组合测试）共用本文件。
let code : (string * string) list =
    [ "fsharp", "let rec fib n =\n    if n < 2 then n else fib (n - 1) + fib (n - 2)\n// 注释 \"引号\" 'c' <T>"
      "F#", "type R = { a: int; b: string }\nlet f (x: R) = $\"{x.a} -> {x.b}\""
      "python", "def f(x: int) -> str:\n    \"\"\"doc\"\"\"\n    return f\"{x!r} & <tag>\"\n@decorator\nclass A(B): pass"
      "ts", "interface I<T> { x?: T }\nconst f = async (a: number): Promise<void> => { await g(`t${a}`) }"
      "rust", "fn main() -> Result<(), Box<dyn Error>> {\n    let v: Vec<u8> = vec![1, 2];\n    println!(\"{:?}\", v);\n    Ok(())\n}"
      "sh", "#!/bin/bash\nfor f in *.txt; do echo \"$f\" | grep -E '^a' && exit 1; done"
      "sql", "SELECT a, COUNT(*) FROM t WHERE b = 'x' GROUP BY a HAVING COUNT(*) > 1; -- c"
      "json", "{\"a\": [1, 2.5, true, null], \"b\": {\"c\": \"d\"}}"
      "dockerfile", "FROM alpine:3\nRUN apk add --no-cache curl\nCMD [\"sh\"]"
      "haskell", "main :: IO ()\nmain = mapM_ print [x * 2 | x <- [1..10], odd x]"
      "diff", "--- a\n+++ b\n@@ -1 +1 @@\n-old\n+new"
      "yaml", "a: 1\nb:\n  - x\n  - 'y'\n# c"
      "csharp", "public sealed class C : I { public async Task<int> M() => await Task.FromResult(1); }"
      "tex", "\\begin{equation} E = mc^2 \\end{equation}"
      "xml", "<a href=\"x\">t &amp; u</a><!-- c -->"
      "unknownlang", "x = 1"
      "", "plain" ]

let math : (string * bool) list =
    [ "x^2", false
      "\\frac{a}{b}", false
      "\\sqrt{x^2+y^2}", false
      "\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}", true
      "\\left( \\frac{1}{2} \\right)", false
      "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}", true
      "\\int_0^\\infty e^{-x}\\,dx", true
      "\\alpha\\beta\\gamma \\mathbb{R} \\mathcal{F}", false
      "\\text{中文} + 1", false
      "\\frac{", false
      "\\undefinedmacro{x}", false
      "", false ]
