// 金标准生成器：tools/OldShellPalette.fs 是旧 AppShell.PaletteItems / RunPaletteItem 方法体的逐行复制（MainView 私有，无法从旧程序集调用），
// 读到的字段与协作者按旧定义给出。必须在 TZ=UTC 下运行：
//   TZ=UTC dotnet fsi compose/CommandPalette/tools/gen-golden.fsx
#load "OldShellPalette.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(__SOURCE_DIRECTORY__ + "/../tests/golden/expected.txt", Render.all ())
