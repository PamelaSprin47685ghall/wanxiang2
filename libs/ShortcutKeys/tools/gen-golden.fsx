// 金标准生成器：用旧仓库 extract-ledger 构建出的程序集跑旧代码。
// 先在旧仓库执行 `dotnet build tests/Wanxiang.Tests`，再把下面 #I 改成那个 bin 目录，然后 `dotnet fsi libs/ShortcutKeys/tools/gen-golden.fsx`。
#I "../../../../wanxiang/tests/Wanxiang.Tests/bin/Debug/net10.0"
#r "Avalonia.Base.dll"
#r "Wanxiang.Core.dll"
#r "Wanxiang.UI.dll"
open System
open Avalonia.Input
open Wanxiang.UI
let mods =
    [ KeyModifiers.None; KeyModifiers.Control; KeyModifiers.Meta; KeyModifiers.Shift; KeyModifiers.Alt
      KeyModifiers.Control ||| KeyModifiers.Shift; KeyModifiers.Meta ||| KeyModifiers.Shift; KeyModifiers.Control ||| KeyModifiers.Alt ]
let keys = Enum.GetValues<Key>() |> Array.distinct
let lines =
    [ for k in keys do
        for m in mods do
            let e = KeyEventArgs(Key = k, KeyModifiers = m)
            let ctrl = m.HasFlag KeyModifiers.Control || m.HasFlag KeyModifiers.Meta
            let shift = m.HasFlag KeyModifiers.Shift
            sprintf "%s %b %b %b => %A" (string k) ctrl shift (m = KeyModifiers.None) (ShortcutRouter.resolve e) ]
IO.File.WriteAllLines(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", lines)
