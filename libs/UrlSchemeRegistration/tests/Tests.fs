module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接`` () =
    let entry = UrlSchemeRegistration.desktopEntry "/opt/wanxiang/wanxiang"
    Assert.Contains("MimeType=x-scheme-handler/wanxiang;", entry)
    Assert.Contains("Exec=/opt/wanxiang/wanxiang %u", entry)
    Assert.Contains("NoDisplay=true", entry)
    Assert.Equal("\"/a b/w\"", UrlSchemeRegistration.quote "/a b/w")
