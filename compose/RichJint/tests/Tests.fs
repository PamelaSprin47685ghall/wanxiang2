module Tests

open System.IO
open System.Threading.Tasks
open Xunit
open Wanxiang.App

[<Fact>]
let ``着色与排版金样与旧实现逐字节相同`` () =
    let expected = File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory, "golden", "rich.txt"))
    let actual = Golden.render ()
    if expected <> actual then
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "rich.actual.txt"), actual)
    Assert.Equal(expected, actual)

[<Fact>]
let ``认不出的语言不抛，返回 None`` () =
    let b = JintRichBackend()
    Assert.Equal(None, b.HighlightHtml("x", "nosuchlanguage"))

[<Fact>]
let ``预热之后结果不变，预热可重复`` () =
    let cold = JintRichBackend().HighlightHtml("let x = 1", "javascript")
    let b = JintRichBackend()
    b.Warmup()
    b.Warmup()
    Assert.Equal(cold, b.HighlightHtml("let x = 1", "javascript"))

[<Fact>]
let ``后台预热与 UI 线程着色并发也安全`` () =
    let b = JintRichBackend()
    let warm = Task.Run(fun () -> b.Warmup())
    let results = [| for i in 1..8 -> Task.Run(fun () -> b.HighlightHtml(sprintf "let x = %d" i, "javascript")) |]
    warm.Wait()
    for r in results do Assert.True(r.Result.IsSome)
    Assert.True((b.MathHtml("x^2", false)).IsSome)
