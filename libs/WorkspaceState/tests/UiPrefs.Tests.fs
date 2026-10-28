module UiPrefsTests

open Xunit
open Wanxiang.UI

// 与旧 Tokens / LayoutPolicy 相同的数值（宿主传入的那一份）。
let limits =
    {
        sidebarWidth = 284.0
        sidebarMinWidth = 232.0
        sidebarMaxWidth = 420.0
        windowMinWidth = 700.0
    }

[<Fact>]
let ``ThemePreference conversions and resolveTheme`` () =
    Assert.Equal("system", ThemePreference.toText FollowSystem)
    Assert.Equal("light", ThemePreference.toText AlwaysLight)
    Assert.Equal("dark", ThemePreference.toText AlwaysDark)

    Assert.Equal(FollowSystem, ThemePreference.ofText "system")
    Assert.Equal(AlwaysLight, ThemePreference.ofText "light")
    Assert.Equal(AlwaysDark, ThemePreference.ofText "dark")

    let prefs =
        { UiPrefs.defaults limits with
            theme = FollowSystem
        }

    Assert.True(UiPrefs.isDark prefs true)
    Assert.False(UiPrefs.isDark prefs false)

    let darkPrefs =
        { UiPrefs.defaults limits with
            theme = AlwaysDark
        }

    Assert.True(UiPrefs.isDark darkPrefs false)

// 迁自旧 ComposerReliabilityTests（文件读写换成文本往返）。
[<Fact>]
let ``persisting drafts is on by default and an old ui file without the key keeps it on`` () =
    Assert.True (UiPrefs.defaults limits).persistDrafts
    let old = """{"theme":"dark","fontScale":15}"""
    Assert.True((UiPrefs.parse limits (Some old)).persistDrafts)

    let saved =
        UiPrefs.render
            (Some old)
            { UiPrefs.defaults limits with
                persistDrafts = false
            }

    Assert.False((UiPrefs.parse limits (Some saved)).persistDrafts)

// 迁自旧 AppShell.CycleTheme：与原样摘录逐例比对。
[<Fact>]
let ``theme cycle and notice match old CycleTheme`` () =
    let old (theme: ThemePreference) =
        let nextTheme =
            match theme with
            | FollowSystem -> AlwaysLight
            | AlwaysLight -> AlwaysDark
            | AlwaysDark -> FollowSystem

        nextTheme,
        (match nextTheme with
         | FollowSystem -> "已切换为跟随系统主题"
         | AlwaysDark -> "已切换为深色主题"
         | AlwaysLight -> "已切换为浅色主题")

    for t in [ FollowSystem; AlwaysLight; AlwaysDark ] do
        let n = ThemePreference.next t
        Assert.Equal(old t, (n, ThemePreference.switchedText n))

    Assert.Equal(
        FollowSystem,
        FollowSystem
        |> ThemePreference.next
        |> ThemePreference.next
        |> ThemePreference.next
    )
