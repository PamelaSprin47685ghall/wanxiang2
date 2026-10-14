module Wanxiang2.ListEmptyCard.Tests.Tests

open System
open Avalonia.Controls
open Xunit
open Wanxiang2.ListEmptyCard
open Wanxiang2.ListEmptyCard.Tests.Probe

let private make () =
    let look, cells = Fixture.make ()
    let c = ListEmptyCard look
    host c.View |> ignore
    c, cells

[<Fact>]
let ``card swaps only on style flip and keeps the shared action button`` () =
    Headless.run (fun () ->
        let c, _ = make ()
        Assert.False c.IsVisible
        c.SetAction "连接主机"
        c.Show(EmptyCardStyle.Note, "", "连接主机后，对话记录会显示在这里。", false, true)
        Assert.Contains("连接主机后", textOf c.View)
        let n = ref 0
        c.ActionClicked.Add(fun () -> n.Value <- n.Value + 1)
        click c.ActionButton
        Assert.Equal(1, n.Value)
        let child = (c.View :?> Border).Child
        c.Show(EmptyCardStyle.Note, "", "另一句", false, false)
        Assert.Same(child, (c.View :?> Border).Child)
        c.Show(EmptyCardStyle.Brand, "正在加载对话…", "", true, false)
        Assert.Contains("正在加载对话…", textOf c.View)
        Assert.True c.Skeleton.IsVisible
        Assert.False c.ActionButton.IsVisible
        c.Show(EmptyCardStyle.Plain, "还没有对话", "发送第一条消息后，对话会显示在这里。", false, false)
        Assert.Contains("还没有对话", textOf c.View)
        c.Hide()
        Assert.False c.IsVisible)

[<Fact>]
let ``skeleton bars follow theme cells`` () =
    Headless.run (fun () ->
        let c, cells = make ()
        c.Show(EmptyCardStyle.Brand, "t", "", true, false)
        cells["SkeletonBarHeight"].Set 12.0
        let bars = descendants c.Skeleton |> Seq.choose (function :? Border as b when not (isNull b.Background) -> Some b | _ -> None) |> List.ofSeq
        Assert.Equal(3, bars.Length)
        Assert.All(bars, fun b -> Assert.Equal(12.0, b.Height)))
