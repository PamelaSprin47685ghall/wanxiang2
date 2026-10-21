module GenerationDraftProbeFailureKindTests
open Xunit
open Wanxiang.UI

// 与主机错误码的一一对应在组合者 Catalog 用真实 ProviderCatalog 核对；这里只验解析本身。
[<Fact>]
let ``codes parse to kinds and legacy text stays unclassified`` () =
    Assert.Equal((ProbeInvalidKey, "说明"), ProbeFailureKind.parse "[invalid-key] 说明")
    Assert.Equal((ProbeTimedOut, "x"), ProbeFailureKind.parse "[timeout]x")
    Assert.Equal((ProbeUnclassified, "说明"), ProbeFailureKind.parse "[nope] 说明")
    // 迁自旧 OnboardingConfigTests `错误码编码解码往返…` 的旧主机部分
    Assert.Equal((ProbeUnclassified, "HTTP 401 · 凭据被拒绝"), ProbeFailureKind.parse "HTTP 401 · 凭据被拒绝")
    Assert.Equal((ProbeUnclassified, "[]"), ProbeFailureKind.parse "[]")
    Assert.Equal((ProbeUnclassified, ""), ProbeFailureKind.parse null)
    Assert.True(ProbeFailureKind.suggestsTransportSwitch ProbeEndpointNotFound)
    Assert.True(ProbeFailureKind.suggestsTransportSwitch ProbeBadResponse)
    Assert.False(ProbeFailureKind.suggestsTransportSwitch ProbeInvalidKey)
