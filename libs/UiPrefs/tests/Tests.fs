module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 与旧 Tokens / LayoutPolicy 相同的数值（宿主传入的那一份）。
let limits = { sidebarWidth = 284.0; sidebarMinWidth = 232.0; sidebarMaxWidth = 420.0; windowMinWidth = 700.0 }

// 金标准：golden/expected.txt 由旧 UiPrefs.load/save 经真实文件跑同一份 Render.fs 生成。
[<Fact>]
let ``golden: parse and render match old load and save byte for byte`` () =
    let dir = Path.Combine(AppContext.BaseDirectory, "golden")
    let inputs = File.ReadAllLines(Path.Combine(dir, "inputs.txt")) |> Array.filter ((<>) "") |> List.ofArray
    let actual = Render.all (UiPrefs.parse limits) UiPrefs.render inputs
    Assert.Equal(File.ReadAllText(Path.Combine(dir, "expected.txt")), actual)

// 迁自旧 UiPureLogicTests（resolveTheme 换成 isDark：ThemeMode 属设计层）。
[<Fact>]
let ``ThemePreference conversions and resolveTheme`` () =
    Assert.Equal("system", ThemePreference.toText FollowSystem)
    Assert.Equal("light", ThemePreference.toText AlwaysLight)
    Assert.Equal("dark", ThemePreference.toText AlwaysDark)

    Assert.Equal(FollowSystem, ThemePreference.ofText "system")
    Assert.Equal(AlwaysLight, ThemePreference.ofText "light")
    Assert.Equal(AlwaysDark, ThemePreference.ofText "dark")

    let prefs = { UiPrefs.defaults limits with theme = FollowSystem }
    Assert.True(UiPrefs.isDark prefs true)
    Assert.False(UiPrefs.isDark prefs false)

    let darkPrefs = { UiPrefs.defaults limits with theme = AlwaysDark }
    Assert.True(UiPrefs.isDark darkPrefs false)

// 迁自旧 ComposerReliabilityTests（文件读写换成文本往返）。
[<Fact>]
let ``persisting drafts is on by default and an old ui file without the key keeps it on`` () =
    Assert.True (UiPrefs.defaults limits).persistDrafts
    let old = """{"theme":"dark","fontScale":15}"""
    Assert.True((UiPrefs.parse limits (Some old)).persistDrafts)
    let saved = UiPrefs.render (Some old) { UiPrefs.defaults limits with persistDrafts = false }
    Assert.False((UiPrefs.parse limits (Some saved)).persistDrafts)
