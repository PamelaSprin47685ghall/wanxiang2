module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``空路径被拒绝`` () =
    Assert.Throws<ArgumentException>(fun () -> LocalState.requireOutsideGit "" |> ignore)
    |> ignore

    Assert.Throws<ArgumentException>(fun () -> LocalState.requireOutsideGit "   " |> ignore)
    |> ignore

[<Fact>]
let ``含 .git 段的路径被拒绝（大小写不敏感）`` () =
    Assert.Throws<ArgumentException>(fun () -> LocalState.requireOutsideGit "/tmp/wanxiang/.git/config" |> ignore)
    |> ignore

    Assert.Throws<ArgumentException>(fun () -> LocalState.requireOutsideGit "/tmp/wanxiang/.GIT/config" |> ignore)
    |> ignore

[<Fact>]
let ``普通路径返回规范化绝对路径`` () =
    let path =
        Path.Combine(Path.GetTempPath(), "wanxiang-localstate-plain-" + Guid.NewGuid().ToString("N"))

    let result = LocalState.requireOutsideGit path
    Assert.Equal(Path.GetFullPath path, result)

[<Fact>]
let ``符号链接指回 .git 内部被拒绝`` () =
    let tmp =
        Path.Combine(Path.GetTempPath(), "wanxiang-localstate-link-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(Path.Combine(tmp, ".git")) |> ignore

    Directory.CreateSymbolicLink(Path.Combine(tmp, "link"), Path.Combine(tmp, ".git"))
    |> ignore

    try
        Assert.Throws<ArgumentException>(fun () ->
            LocalState.requireOutsideGit (Path.Combine(tmp, "link", "objects")) |> ignore)
        |> ignore
    finally
        Directory.Delete(tmp, true)

[<Fact>]
let ``符号链接指向普通目录时解析为真实路径`` () =
    let tmp =
        Path.Combine(Path.GetTempPath(), "wanxiang-localstate-resolve-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(Path.Combine(tmp, "real")) |> ignore

    Directory.CreateSymbolicLink(Path.Combine(tmp, "link"), Path.Combine(tmp, "real"))
    |> ignore

    try
        let result = LocalState.requireOutsideGit (Path.Combine(tmp, "link", "file"))
        Assert.Equal(Path.GetFullPath(Path.Combine(tmp, "real", "file")), result)
    finally
        Directory.Delete(tmp, true)
