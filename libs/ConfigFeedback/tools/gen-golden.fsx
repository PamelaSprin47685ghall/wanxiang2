// 金标准生成器：tools/OldConfigApplied.fs 是旧 AppShell ConfigApplied 分支的原样摘录（MainView 私有），用它跑测试共用的 Render.fs：
//   dotnet fsi libs/ConfigFeedback/tools/gen-golden.fsx
#load "OldConfigApplied.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
