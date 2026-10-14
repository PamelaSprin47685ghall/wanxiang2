// 金标准生成器：tools/OldPendingDeletion.fs 是从旧 AppShell.fs 原样摘出的私有成员，用它跑测试共用的 Render.fs：
//   dotnet fsi libs/PendingDeletion/tools/gen-golden.fsx
#load "OldPendingDeletion.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
