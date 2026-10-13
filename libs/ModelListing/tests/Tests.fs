module Tests
open Xunit
open Wanxiang.Server

[<Fact>]
let ``模型清单解析`` () =
    Assert.Equal(Ok ["a"; "b"], ModelListing.modelIdsFromProbe """{"data":[{"id":"a"},{"id":"b"}]}""")
    Assert.True(ModelListing.ofOllamaVersion """{"version":"0.5.1"}""")
    Assert.Equal(Some true, ModelListing.anthropicKnown "claude-3-5-sonnet")
