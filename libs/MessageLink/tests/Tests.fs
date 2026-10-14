module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 ContinueLinkUndoTests。
[<Fact>]
let ``231 消息链接两种写法往返，普通搜索词不误认`` () =
    let conv = Guid.NewGuid()
    let desktop = MessageLink.format None conv 42UL
    Assert.StartsWith("wanxiang://open?", desktop)
    Assert.Equal(Some(conv, 42UL), MessageLink.tryParse desktop)
    let web = MessageLink.format (Some "https://host.example/app/#old") conv 7UL
    Assert.Equal(sprintf "https://host.example/app/#open?c=%O&m=7" conv, web)
    Assert.Equal(Some(conv, 7UL), MessageLink.tryParse ("  " + web + " "))
    Assert.Equal(None, MessageLink.tryParse "如何打开链接")
    Assert.Equal(None, MessageLink.tryParse "https://example.com/#open?c=bad&m=1")
    Assert.Equal(None, MessageLink.tryParse "https://example.com/page")

[<Fact>]
let ``exact desktop link text and rejections`` () =
    let conv = Guid.Parse "11111111-2222-3333-4444-555555555555"
    Assert.Equal("wanxiang://open?c=11111111-2222-3333-4444-555555555555&m=9", MessageLink.format None conv 9UL)
    Assert.Equal("wanxiang://open?c=11111111-2222-3333-4444-555555555555&m=9", MessageLink.format (Some "file:///x") conv 9UL)
    Assert.Equal(None, MessageLink.tryParse "wanxiang://open?c=00000000-0000-0000-0000-000000000000&m=1")
    Assert.Equal(None, MessageLink.tryParse "wanxiang://open?c=11111111-2222-3333-4444-555555555555")
    Assert.Equal(Some(conv, 3UL), MessageLink.tryParse "WANXIANG://open?m=3&c=11111111-2222-3333-4444-555555555555")
