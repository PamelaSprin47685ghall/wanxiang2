module DeviceListJsonTests
open System
open Xunit
open Wanxiang.Server

// 外部线协议字节向量：设备列表 JSON 是对外契约，这些逐字节断言保留。
[<Fact>]
let ``设备条目形状与旧实现一致`` () =
    let t = DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
    let a = DeviceListJson.item "d1" "手机" t (Some t) (Set.ofList [ DeviceFact.Online; DeviceFact.Current ])
    Assert.Equal("""{"id":"d1","name":"\u624B\u673A","createdAt":"2026-01-02T03:04:05.0000000Z","lastSeen":"2026-01-02T03:04:05.0000000Z","revoked":false,"online":true,"current":true,"desktop":false}""", a.ToJsonString())
    let b = DeviceListJson.item "d2" "x" t None (Set.ofList [ DeviceFact.Revoked; DeviceFact.Online; DeviceFact.Desktop ])
    Assert.Equal("""{"id":"d2","name":"x","createdAt":"2026-01-02T03:04:05.0000000Z","revoked":true,"online":false,"current":false,"desktop":true}""", b.ToJsonString())

[<Fact>]
let ``吊销的设备即使在线也报 offline`` () =
    let t = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let a = DeviceListJson.item "d" "n" t None (Set.ofList [ DeviceFact.Revoked; DeviceFact.Online ])
    Assert.False(a["online"].GetValue<bool>())
    Assert.True(a["revoked"].GetValue<bool>())

[<Fact>]
let ``没有 lastSeen 时不输出该字段，时间统一转 UTC`` () =
    let t = DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours 8.0)
    let a = DeviceListJson.item "d" "n" t None Set.empty
    Assert.False(a.ContainsKey "lastSeen")
    Assert.Equal("2026-01-01T00:00:00.0000000Z", a["createdAt"].GetValue<string>())

[<Fact>]
let ``字段顺序固定`` () =
    let t = DateTimeOffset.UnixEpoch
    let a = DeviceListJson.item "d" "n" t (Some t) Set.empty
    Assert.Equal<string list>([ "id"; "name"; "createdAt"; "lastSeen"; "revoked"; "online"; "current"; "desktop" ], [ for kv in a -> kv.Key ])
