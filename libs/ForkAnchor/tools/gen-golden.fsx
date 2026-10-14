// 金标准生成器：tools/OldForkAnchor.fs 是从旧仓库 extract-ledger a5a1afc 的 AppShell.ForkFrom 原样摘出的代码
// （MainView 私有成员，无法从程序集直接调用），用它跑测试共用的 Render.fs：
//   dotnet fsi libs/ForkAnchor/tools/gen-golden.fsx
#load "OldForkAnchor.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
