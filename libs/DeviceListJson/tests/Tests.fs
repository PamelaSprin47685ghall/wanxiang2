module Tests

open System
open Xunit
open Wanxiang.Server

[<Fact>]
let ``设备条目形状与旧实现一致`` () =
    let t = DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
    let a = DeviceListJson.item "d1" "手机" t (Some t) false true true false
    Assert.Equal("""{"id":"d1","name":"\u624B\u673A","createdAt":"2026-01-02T03:04:05.0000000Z","lastSeen":"2026-01-02T03:04:05.0000000Z","revoked":false,"online":true,"current":true,"desktop":false}""", a.ToJsonString())
    let b = DeviceListJson.item "d2" "x" t None true true false true
    Assert.Equal("""{"id":"d2","name":"x","createdAt":"2026-01-02T03:04:05.0000000Z","revoked":true,"online":false,"current":false,"desktop":true}""", b.ToJsonString())
