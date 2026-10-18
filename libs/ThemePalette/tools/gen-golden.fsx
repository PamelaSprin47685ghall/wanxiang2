// 金样生成器：照抄的旧 Design/Palette.fs（tools/OldPalette.fs）逐项输出。
// 用法：dotnet fsi tools/gen-golden.fsx（需要本机 NuGet 缓存里有 Avalonia 12.1.2）
#r "nuget: Avalonia, 12.1.2"
#load "OldPalette.fs" "../tests/Render.fs"
open OldPalette
let keys = System.IO.File.ReadAllLines(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "keys.txt")) |> List.ofArray
// 旧配色没有 ControlStroke（2026-10-18 本仓库补的控件边界描边，≥3:1；见 AGENTS.md 与 tests/Tests.fs）。
// 与 MotionSwitch 同一先例：这一项写死在生成器里，其余仍由旧配色生成，重跑不覆盖新令牌。
let color mode key =
    if key = "ControlStroke" then
        Avalonia.Media.Color.Parse(if mode = "Light" then "#8D8A7E" else "#76767F")
    else Adapter.pick (Palette.ofMode (if mode = "Light" then Light else Dark)) key
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "themepalette.txt"), Render.render keys color)
