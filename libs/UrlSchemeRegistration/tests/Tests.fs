module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

// 迁自旧测试。
[<Fact>]
let ``251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接`` () =
    let entry = UrlSchemeRegistration.desktopEntry "/opt/wanxiang/wanxiang"
    Assert.Contains("MimeType=x-scheme-handler/wanxiang;", entry)
    Assert.Contains("Exec=/opt/wanxiang/wanxiang %u", entry)
    Assert.Contains("NoDisplay=true", entry)
    Assert.Equal("\"/a b/w\"", UrlSchemeRegistration.quote "/a b/w")
