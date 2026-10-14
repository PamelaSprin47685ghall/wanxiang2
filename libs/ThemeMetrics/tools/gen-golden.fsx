// 金样生成器：照抄的旧数值（tools/OldMetrics.fs）逐项输出。用法：dotnet fsi tools/gen-golden.fsx
#load "OldMetrics.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "thememetrics.txt"), Render.render OldMetrics.Adapter.all)
