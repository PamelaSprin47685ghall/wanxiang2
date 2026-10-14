// 金标准生成器：用旧仓库 extract-ledger 构建出的程序集跑旧代码。
// 先在旧仓库执行 `dotnet build tests/Wanxiang.Tests`，再把下面 #I 改成那个 bin 目录，然后 `dotnet fsi libs/DraftStore/tools/gen-golden.fsx`。
#I "../../../../wanxiang/tests/Wanxiang.Tests/bin/Debug/net10.0"
#r "Wanxiang.Core.dll"
#r "Wanxiang.UI.dll"
#load "../tests/Render.fs"
open Wanxiang.UI
let saves (xs: DraftSnapshot list) =
    let backend = MemoryDraftBackend()
    let store = DraftStore(backend, fun () -> true)
    [ for s in xs ->
        let before = backend.Stored
        store.Save(s).Wait()
        match before, backend.Stored with
        | b, a when b = a -> "Unchanged"
        | _, None -> "Delete"
        | _, Some t -> "Write " + t ]
let d = __SOURCE_DIRECTORY__ + "/../tests/golden/"
let lines = System.IO.File.ReadAllLines(d + "inputs.jsonl") |> Array.filter ((<>) "") |> List.ofArray
System.IO.File.WriteAllText(d + "expected.txt", Render.all lines saves)
