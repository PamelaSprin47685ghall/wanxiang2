module OlderPageLoadTests
open System
open Xunit
open Wanxiang.UI

// 旧 AppShell.CheckOlderPageTimeout / FailOlderPage 原样摘录（字段换成参数，失败条换成返回文案）。
let private oldCheck (pageLoading: bool) (pageRequestedAt: DateTimeOffset option) (now: DateTimeOffset) (olderPageTimeout: TimeSpan) =
    let mutable failed = None
    let failOlderPage (message: string) = if pageLoading || pageRequestedAt.IsSome then failed <- Some message
    match pageRequestedAt with
    | Some at when pageLoading && now - at >= olderPageTimeout ->
        failOlderPage "加载更早消息超时，主机没有回应。可以点「重试」。"
    | _ -> ()
    failed

[<Fact>]
let ``timeout check matches old AppShell`` () =
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let timeout = TimeSpan.FromSeconds 15.0
    for loading in [ false; true ] do
        for at in [ None; Some t0 ] do
            for secs in [ 0.0; 14.999; 15.0; 60.0 ] do
                let now = t0.AddSeconds secs
                let actual =
                    if OlderPageLoad.timedOut loading at now timeout && OlderPageLoad.inFlight loading at then
                        Some OlderPageLoad.timeoutText
                    else None
                Assert.Equal(oldCheck loading at now timeout, actual)

[<Fact>]
let ``in flight when loading or requested`` () =
    Assert.False(OlderPageLoad.inFlight false None)
    Assert.True(OlderPageLoad.inFlight false (Some DateTimeOffset.UnixEpoch))
    Assert.True(OlderPageLoad.inFlight true None)
    Assert.Equal("还没有连接主机，更早的消息无法加载。连接后再点「重试」。", OlderPageLoad.offlineText)
