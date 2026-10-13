module SandboxFilesTests

open System.IO
open Xunit
open Wanxiang.Server

let private root () =
    let d = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    Directory.CreateDirectory d |> ignore
    d

[<Fact>]
let ``files inside the roots are read and listed`` () =
    let d = root ()
    File.WriteAllText(Path.Combine(d, "a.txt"), "hi")
    Directory.CreateDirectory(Path.Combine(d, "sub")) |> ignore
    Assert.Equal("hi", BuiltinToolsCore.readFile [ d ] (Path.Combine(d, "a.txt")))
    let listing = BuiltinToolsCore.listDirectory [ d ] d
    Assert.Contains("a.txt (2 B)", listing)
    Assert.Contains("sub/", listing)

[<Fact>]
let ``paths outside the roots, empty roots and empty paths are refused`` () =
    let d = root ()
    Assert.StartsWith("error: 路径超出沙箱范围", BuiltinToolsCore.readFile [ d ] (Path.GetTempPath()))
    Assert.StartsWith("error: 文件工具未启用", BuiltinToolsCore.readFile [] d)
    Assert.Equal(Error "路径不能为空。", BuiltinToolsCore.resolveInRoots [ d ] " ")
