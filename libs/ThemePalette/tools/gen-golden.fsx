// 金样生成器：照抄的旧 Design/Palette.fs（tools/OldPalette.fs）逐项输出。
// 用法：dotnet fsi tools/gen-golden.fsx（需要本机 NuGet 缓存里有 Avalonia 12.1.2）
#r "nuget: Avalonia, 12.1.2"
#load "OldPalette.fs" "../tests/Render.fs"
open OldPalette
let keys = System.IO.File.ReadAllLines(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "keys.txt")) |> List.ofArray
let color mode key = Adapter.pick (Palette.ofMode (if mode = "Light" then Light else Dark)) key
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "themepalette.txt"), Render.render keys color)
