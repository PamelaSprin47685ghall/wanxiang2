module GenerationDraftProbeSlotTests

open Xunit
open Wanxiang.UI

// 迁自旧 SettingsViewTests（原用例起无头窗口打开编辑器；这里只留归属与缓存）。
[<Fact>]
let ``SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`` () =
    let slot = ProbeSlot()
    slot.Open "test-prov"
    // 1. 编辑器打开时收到结果：归编辑器，缓存后再开编辑器用缓存
    Assert.True(slot.Claim "test-prov")
    slot.Remember("test-prov", [ "m-a"; "m-b" ])
    Assert.Equal<string list>([ "m-a"; "m-b" ], slot.EffectiveModels("test-prov", [ "old" ]))
    // 2. 编辑器未打开时收到结果：不归任何编辑器，只缓存
    Assert.False(slot.Claim "test-prov")
    slot.Remember("test-prov", [ "bg-model-a" ])
    Assert.Equal<string list>([ "bg-model-a" ], slot.EffectiveModels("test-prov", [ "old" ]))

[<Fact>]
let ``别家的结果不夺归属；关闭编辑器后归属清空`` () =
    let slot = ProbeSlot()
    slot.Open "a"
    Assert.False(slot.Claim "b")
    Assert.Equal(Some "a", slot.Active)
    slot.Close()
    Assert.False(slot.Claim "a")

[<Fact>]
let ``空缓存不覆盖配置；默认模型不在清单里时取第一项`` () =
    let slot = ProbeSlot()
    slot.Remember("a", [])
    Assert.Equal<string list>([ "cfg" ], slot.EffectiveModels("a", [ "cfg" ]))
    Assert.Equal("m2", ProbeSlot.DefaultModel("m2", [ "m1"; "m2" ]))
    Assert.Equal("m1", ProbeSlot.DefaultModel("gone", [ "m1"; "m2" ]))
    Assert.Equal("", ProbeSlot.DefaultModel("gone", []))
