// 金标准生成器：tools/OldUploadPlan.fs 是从旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs 原样摘出的代码（MainView 私有成员，旧程序集里调不到），
// 用它跑测试共用的 Render.fs：
//   dotnet fsi libs/UploadPlan/tools/gen-golden.fsx
#load "OldUploadPlan.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
