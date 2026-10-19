module Wanxiang2.SidebarSearch.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.SidebarSearch
open Wanxiang2.SidebarSearch.Tests.Probe

let private look () =
    let debounce = Cell 0.0
    debounce,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = Cell 14.0; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = Cell 8.0; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      DebounceMs = debounce; GlyphSize = Cell 16.0; IconButtonSize = Cell 30.0; CompactTarget = Cell 44.0; Space6xl = Cell 48.0 }

let private make () =
    let d, l = look ()
    let s = SidebarSearch l
    let panel = StackPanel()
    panel.Children.Add s.View
    panel.Children.Add s.EmptyHint
    let w = host panel
    s, w, d

let private press (w: Window) k =
    w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null)
    w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``Sidebar search clear button has accessible tooltip and automation properties`` () =
    Headless.run (fun () ->
        let s, _, _ = make ()
        let b = (named s.View "清空搜索").Value
        Assert.Equal("清空搜索", ToolTip.GetTip(b) :?> string)
        // 空输入时槽位保留但不可达
        Assert.Equal(0.0, b.Opacity)
        Assert.False b.Focusable
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView b)
        s.Text <- "abc"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(1.0, b.Opacity)
        Assert.True b.Focusable
        let cleared = ref 0
        s.ClearRequested.Add(fun () -> cleared.Value <- cleared.Value + 1)
        click b
        Assert.Equal(1, cleared.Value))

[<Fact>]
let ``Sidebar search navigation moves focus and handles escape`` () =
    Headless.run (fun () ->
        let s, w, _ = make ()
        let seen = ResizeArray<Key>()
        s.Keys.Add(fun a ->
            seen.Add a.Key
            if a.Key = Key.Down then a.Handled <- true)
        s.Focus true
        press w Key.Down
        press w Key.Enter
        press w Key.Escape
        Assert.Equal<Key list>([ Key.Down; Key.Enter; Key.Escape ], List.ofSeq seen))

[<Fact>]
let ``search input debounces and the empty hint clears on click, enter and escape`` () =
    Headless.run (fun () ->
        let s, w, _ = make ()
        let fired = ResizeArray<string>()
        s.Debounced.Add fired.Add
        s.Text <- "a"
        s.Text <- "ab"
        Threading.Thread.Sleep 30
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "ab" ], List.ofSeq fired)
        let cleared = ref 0
        s.ClearRequested.Add(fun () -> cleared.Value <- cleared.Value + 1)
        s.SetEmptyHint true
        w.UpdateLayout()
        click s.EmptyHint
        s.FocusEmptyHint()
        press w Key.Enter
        press w Key.Escape
        Assert.Equal(3, cleared.Value)
        Assert.Equal("未找到匹配对话，点击清空搜索", AutomationProperties.GetName s.EmptyHint))
