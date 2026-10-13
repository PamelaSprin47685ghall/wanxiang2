module Wanxiang.Tests.ProductizationToolTests

// 原样迁自旧仓库 d9651ed tests/Wanxiang.Tests/ProductizationTests.fs「内建工具沙箱」一节（只改了模块名与 open；tempDir 取自旧 Helpers）。

open System
open System.IO
open Xunit
open Wanxiang.Config
open Wanxiang.Server

let private tempDir () =
    let d = Path.Combine(Path.GetTempPath(), "wx-test-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    d

let private cleanup (dir: string) =
    try Directory.Delete(dir, true) with _ -> ()

// ---------------------------------------------------------------- 内建工具沙箱

[<Fact>]
let ``file tools are unavailable without a configured sandbox`` () =
    Assert.Empty(BuiltinTools.all { ToolsConfig.defaults with fileReadRoots = [] }
                 |> List.filter (fun t -> t.Name.StartsWith "builtin_file"))

[<Fact>]
let ``file tools appear once a sandbox root is configured`` () =
    let dir = tempDir ()
    try
        let tools = BuiltinTools.all { ToolsConfig.defaults with fileReadRoots = [ dir ] }
        Assert.Equal(2, tools |> List.filter (fun t -> t.Name.StartsWith "builtin_file") |> List.length)
    finally
        cleanup dir

[<Fact>]
let ``sandbox rejects paths outside every configured root`` () =
    let dir = tempDir ()
    try
        let roots = [ dir ]
        match BuiltinTools.resolveInRoots roots (Path.Combine(dir, "ok.txt")) with
        | Ok _ -> ()
        | Error e -> failwithf "expected the in-sandbox path to be accepted: %s" e
        for escape in [ "/etc/passwd"; Path.Combine(dir, "..", "escape.txt") ] do
            match BuiltinTools.resolveInRoots roots escape with
            | Ok _ -> failwithf "expected %s to be rejected" escape
            | Error _ -> ()
    finally
        cleanup dir

[<Fact>]
let ``stable tool ids round-trip to function names`` () =
    Assert.Equal("builtin_file_read", BuiltinTools.functionName "builtin:file.read")
    Assert.Equal("builtin:file.read", BuiltinTools.stableId "builtin_file_read")
    Assert.Equal("builtin:echo", BuiltinTools.stableId (BuiltinTools.functionName "builtin:echo"))

