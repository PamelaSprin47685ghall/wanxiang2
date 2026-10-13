module ProviderFailureTests

// 全部分支由 AgentRuntime 的金样（旧实现生成）钉住；这里钉两条代表。

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Agent

[<Fact>]
let ``timeouts and refused connections are classified`` () =
    Assert.Equal(ProviderTimeout, (ProviderFailure.classify "P" "m" (TimeoutException "t")).kind)
    let e = ProviderFailure.classify "P" "m" (Exception "connection refused")
    Assert.Equal(ProviderUnavailable, e.kind)
    Assert.True e.retryable
