module Tests

open System.Threading.Tasks
open Xunit
open Wanxiang.UI

// 迁自旧 PartialPersistAndUrlSchemeTests。
[<Fact>]
let ``251 外部链接投给当前订阅者`` () =
    let inbox = Inbox<string>()
    let link = "wanxiang://open?c=queued&m=1"
    let seen = ResizeArray<string>()
    inbox.Subscribe seen.Add
    LinkInbox.post inbox link
    Assert.Contains(link, seen)

[<Fact>]
let ``订阅前的链接排队，订阅时按序一次交付；空白链接不收`` () =
    let inbox = Inbox<string>()
    LinkInbox.post inbox "a"
    LinkInbox.post inbox "  "
    LinkInbox.post inbox null
    LinkInbox.post inbox "b"
    Assert.Equal(2, inbox.Pending)
    let seen = ResizeArray<string>()
    inbox.Subscribe seen.Add
    Assert.Equal<string list>([ "a"; "b" ], List.ofSeq seen)
    Assert.Equal(0, inbox.Pending)

[<Fact>]
let ``多线程投递不丢条目`` () =
    let inbox = Inbox<int>()
    let seen = System.Collections.Concurrent.ConcurrentBag<int>()
    let posts = [ for i in 0..199 -> Task.Run(fun () -> inbox.Post i) ]
    inbox.Subscribe seen.Add
    Task.WaitAll(posts |> Array.ofList)
    Assert.Equal(200, seen.Count)
    Assert.Equal(200, seen |> Seq.distinct |> Seq.length)
