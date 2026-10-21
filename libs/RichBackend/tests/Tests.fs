module Tests

open Xunit
open Wanxiang.UI

type FakeBackend(highlight: string option, math: string option) =
    interface IRichBackend with
        member _.HighlightHtml(_, _) = highlight
        member _.MathHtml(_, _) = math

[<Fact>]
let ``install 后 current 返回同一实例`` () =
    let fake = FakeBackend(Some "<span>", Some "<math>")
    RichBackend.install fake

    match RichBackend.current () with
    | Some value -> Assert.Same(box fake, box value)
    | None -> Assert.Fail("expected a backend after install")

[<Fact>]
let ``install 覆盖前一个后端`` () =
    let first = FakeBackend(None, None)
    let second = FakeBackend(Some "x", None)
    RichBackend.install first
    RichBackend.install second

    match RichBackend.current () with
    | Some value -> Assert.Same(box second, box value)
    | None -> Assert.Fail("expected the second backend")

[<Fact>]
let ``current 返回的后端可以调用接口方法`` () =
    let fake = FakeBackend(Some "<b>hi</b>", Some "<span>m</span>")
    RichBackend.install fake
    let backend = RichBackend.current () |> Option.get
    Assert.Equal(Some "<b>hi</b>", backend.HighlightHtml("code", "fsharp"))
    Assert.Equal(Some "<span>m</span>", backend.MathHtml("x", true))
