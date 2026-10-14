// 金样生成器：旧代码（tools/OldCardReconcile.fs，从旧仓库 extract-ledger 照抄分支）跑测试共用的 Samples 与 Render。
// 用法：dotnet fsi tools/gen-golden.fsx（在本库目录或任意目录均可）
#load "OldCardReconcile.fs" "../tests/Samples.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "cardreconcile.txt"), Render.render ())
