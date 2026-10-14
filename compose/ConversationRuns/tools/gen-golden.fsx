// 金标准生成器：tools/OldGenerationOutcome.fs 是旧 AppShell 分支的原样摘录（MainView 私有，无法从旧程序集直接调用），
// 编译时只借 Wire 的类型（GenerationError，旧仓库已改为引用同一个包）：
//   dotnet build libs/Wire -c Release && dotnet fsi compose/ConversationRuns/tools/gen-golden.fsx
#r "../../../libs/Wire/bin/Release/net10.0/Wire.dll"
#load "OldGenerationOutcome.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
