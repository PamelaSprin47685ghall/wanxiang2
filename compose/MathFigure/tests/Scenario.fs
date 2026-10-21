module Scenario

/// 金样输入（旧代码生成器与新组合测试共用）：TeX、是否展示式。
let math: (string * bool) list =
    [
        "x^2", false
        "\\frac{a}{b}", false
        "\\sqrt{x^2+y^2}", false
        "\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}", true
        "\\left( \\frac{1}{2} \\right)", false
        "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}", true
        "\\int_0^\\infty e^{-x}\\,dx", true
        "\\alpha\\beta\\gamma \\mathbb{R} \\mathcal{F}", false
        "\\text{中文} + 1", false
        "a \\pm b - c", false
        "\\frac{", false
    ]
