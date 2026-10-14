// 金标准生成器：用旧仓库 extract-ledger 构建出的程序集跑旧代码。
// 先在旧仓库执行 `dotnet build tests/Wanxiang.Tests`，再把下面 #I 改成那个 bin 目录，然后 `dotnet fsi libs/UiPrefs/tools/gen-golden.fsx`。
#I "../../../../wanxiang/tests/Wanxiang.Tests/bin/Debug/net10.0"
#r "Wanxiang.Core.dll"
#r "Wanxiang.UI.dll"
#load "../tests/Render.fs"
open System.IO
open Wanxiang.UI
let home = Path.Combine(Path.GetTempPath(), "w2-uiprefs-gold")
if Directory.Exists home then Directory.Delete(home, true)
Directory.CreateDirectory home |> ignore
System.Environment.SetEnvironmentVariable("WANXIANG_HOME", home)
let path = Path.Combine(home, "ui.json")
let put (text: string option) = match text with Some t -> File.WriteAllText(path, t) | None -> (if File.Exists path then File.Delete path)
let load text = put text; UiPrefs.load ()
let save text p = put text; UiPrefs.save p; File.ReadAllText path
let d = __SOURCE_DIRECTORY__ + "/../tests/golden/"
let inputs = File.ReadAllLines(d + "inputs.txt") |> Array.filter ((<>) "") |> List.ofArray
File.WriteAllText(d + "expected.txt", Render.all load save inputs)
