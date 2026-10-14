// 金标准生成器：用旧仓库 extract-ledger 构建出的程序集跑旧代码：
//   dotnet fsi libs/ClientConnections/tools/gen-golden.fsx
// 先在旧仓库执行 `dotnet build tests/Wanxiang.Tests`，必要时把下面 #I 改成那个 bin 目录。
#I "../../../../wanxiang/tests/Wanxiang.Tests/bin/Debug/net10.0"
#r "Wanxiang.Core.dll"
#r "Wanxiang.UI.dll"
#load "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
