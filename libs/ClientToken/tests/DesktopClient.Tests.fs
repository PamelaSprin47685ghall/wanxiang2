module DesktopClientTests

open Xunit
open DesktopClient

// SPEC 1：保留名不分大小写、去空白；配对设备取不到它
[<Fact>]
let ``reserved name cannot be taken by a paired device`` () =
    Assert.True(DesktopClient.isReservedName " Wanxiang-Desktop ")
    Assert.False(DesktopClient.isReservedName null)
    Assert.Equal("fb", DesktopClient.sanitizeName (Some "wanxiang-desktop") "fb")
    Assert.Equal("fb", DesktopClient.sanitizeName (Some "  ") "fb")
    Assert.Equal("fb", DesktopClient.sanitizeName None "fb")
    Assert.Equal("phone", DesktopClient.sanitizeName (Some " phone ") "fb")

// SPEC 2：桌面身份 = 保留名 + 未吊销 + 回环地址（含 IPv4 映射）
[<Fact>]
let ``desktop identity needs reserved name, not revoked, loopback`` () =
    Assert.True(DesktopClient.isDesktop "wanxiang-desktop" false "127.0.0.1")
    Assert.True(DesktopClient.isDesktop "wanxiang-desktop" false "::1")
    Assert.True(DesktopClient.isDesktop "wanxiang-desktop" false "::ffff:127.0.0.1")
    Assert.False(DesktopClient.isDesktop "wanxiang-desktop" true "127.0.0.1")
    Assert.False(DesktopClient.isDesktop "wanxiang-desktop" false "192.168.1.2")
    Assert.False(DesktopClient.isDesktop "phone" false "127.0.0.1")
    Assert.False(DesktopClient.isDesktop "wanxiang-desktop" false "")
