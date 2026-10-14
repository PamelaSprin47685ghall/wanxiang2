module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧 ShortcutRouter.resolve 对 Avalonia 全部按键 × 8 种修饰键组合生成，
// 每行是「按键名 ctrl shift 无修饰 => 意图」；这里用同样的四项喂 resolveKey，结果逐行相同。
[<Fact>]
let ``golden: every key and modifier combination resolves like the old router`` () =
    let lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt"))
    Assert.True(lines.Length > 1000)
    for line in lines do
        let input, expected = let i = line.IndexOf " => " in line.Substring(0, i), line.Substring(i + 4)
        let parts = input.Split ' '
        let key = { key = parts[0]; ctrl = bool.Parse parts[1]; shift = bool.Parse parts[2]; noModifiers = bool.Parse parts[3] }
        Assert.True((expected = sprintf "%A" (ShortcutRouter.resolveKey key)), line)
