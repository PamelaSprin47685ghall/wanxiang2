module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/OldForkAnchor.fs（旧 ForkFrom 原样摘录）跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

[<Fact>]
let ``fork point is the last commit before the target, or the last commit`` () =
    Assert.Equal(Some 3UL, ForkAnchor.forkAfter [ 1UL; 3UL; 5UL ] (Some 5UL))
    Assert.Equal(Some 5UL, ForkAnchor.forkAfter [ 1UL; 3UL; 5UL ] None)
    Assert.Equal(None, ForkAnchor.forkAfter [ 1UL; 3UL; 5UL ] (Some 1UL))

[<Fact>]
let ``edited message wire bytes`` () =
    Assert.Equal("""{"role":"user","contents":[{"text":"hi"}]}""", (ForkAnchor.editedMessage "hi").ToJsonString())
