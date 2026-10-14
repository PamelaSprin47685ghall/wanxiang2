// 金样生成器：旧代码（tools/Old*.fs，从旧仓库 extract-ledger 原样摘出或照抄分支）跑测试共用的 Samples 与 Render。
// 用法：dotnet fsi tools/gen-golden.fsx（在本库目录或任意目录均可）
// ConnectionIssue 取本仓库 libs/ConnectionIssue 的源码（只供生成器用；组合本身用包）
#load "../../../libs/ConnectionIssue/ConnectionIssue.fs" "OldDialogs.fs" "OldConnectIssueText.fs" "../tests/Samples.fs" "../tests/Render.fs"
System.IO.File.WriteAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "tests", "golden", "connectissuetext.txt"), Render.render ())
