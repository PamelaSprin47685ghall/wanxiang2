module Tests

open System
open System.IO
open Xunit
open Wanxiang.App

[<Fact>]
let ``没有参数时全是默认`` () = Assert.Equal(Cli.defaults, Cli.parse [||])

[<Fact>]
let ``等号与空格两种写法都支持`` () =
    let a = Cli.parse [| "--config=/a.toml"; "--data"; "/d"; "--bundle=/b" |]
    Assert.Equal(Some "/a.toml", a.config)
    Assert.Equal(Some "/d", a.data)
    Assert.Equal(Some "/b", a.bundle)

[<Fact>]
let ``开关：裸写为真，等号后按布尔词解析，认不出的词为 None`` () =
    let a = Cli.parse [| "--server"; "--client=off"; "--pwa=YES"; "--fix=maybe" |]
    Assert.Equal(Some true, a.server)
    Assert.Equal(Some false, a.client)
    Assert.Equal(Some true, a.pwa)
    Assert.Equal(None, a.fix)
    Assert.Equal(Some true, (Cli.parse [| "--fix" |]).fix)

[<Fact>]
let ``后出现的同名参数覆盖前面的`` () =
    Assert.Equal(Some false, (Cli.parse [| "--server"; "--server=0" |]).server)

[<Fact>]
let ``wanxiang 链接不区分大小写，作为 openUrl`` () =
    Assert.Equal(Some "WanXiang://pair?x=1", (Cli.parse [| "WanXiang://pair?x=1" |]).openUrl)

[<Fact>]
let ``未知参数与缺值抛出，文字与旧实现相同`` () =
    Assert.Equal("unknown argument: --nope", (Assert.ThrowsAny<exn>(fun () -> Cli.parse [| "--nope" |] |> ignore)).Message)
    Assert.Equal("missing value for --config", (Assert.ThrowsAny<exn>(fun () -> Cli.parse [| "--config" |] |> ignore)).Message)

[<Fact>]
let ``help 立即返回，后面的参数不再解析`` () =
    let a = Cli.parse [| "-h"; "--nope" |]
    Assert.True a.help
    Assert.True((Cli.parse [| "--server"; "--help" |]).help)

[<Fact>]
let ``路径：默认配置在 home 下，数据目录在配置旁`` () =
    let home = Path.Combine(Path.GetTempPath(), "wxhome")
    let c, d = Cli.paths Cli.defaults home
    Assert.Equal(Path.Combine(home, "config.toml"), c)
    Assert.Equal(Path.Combine(home, "data"), d)
    let c2, d2 = Cli.paths { Cli.defaults with config = Some "/x/y/c.toml" } home
    Assert.Equal(Path.GetFullPath "/x/y/c.toml", c2)
    Assert.Equal(Path.GetFullPath "/x/y/data", d2)

[<Fact>]
let ``WANXIANG_HOME 优先，空白视为未设置`` () =
    let old = Environment.GetEnvironmentVariable "WANXIANG_HOME"
    try
        Environment.SetEnvironmentVariable("WANXIANG_HOME", "/custom")
        Assert.Equal("/custom", Cli.defaultHome ())
        Environment.SetEnvironmentVariable("WANXIANG_HOME", "  ")
        Assert.EndsWith(Path.Combine(".config", "wanxiang"), Cli.defaultHome ())
    finally Environment.SetEnvironmentVariable("WANXIANG_HOME", old)
