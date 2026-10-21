module Tests

open Xunit
open Wanxiang.UI

[<Fact>]
let ``ShortcutRouter resolveKey resolves basic key combinations correctly`` () =
    let k key ctrl shift noMod =
        {
            key = key
            ctrl = ctrl
            shift = shift
            noModifiers = noMod
        }

    Assert.Equal(ShortcutAction.Escape, ShortcutRouter.resolveKey (k "Escape" false false false))
    Assert.Equal(ShortcutAction.ToggleSidebar, ShortcutRouter.resolveKey (k "B" true false false))
    Assert.Equal(ShortcutAction.CloseWindow, ShortcutRouter.resolveKey (k "W" true false false))
    Assert.Equal(ShortcutAction.NewConversation, ShortcutRouter.resolveKey (k "N" true false false))
    Assert.Equal(ShortcutAction.OpenCommandPalette, ShortcutRouter.resolveKey (k "K" true false false))
    Assert.Equal(ShortcutAction.FocusSearch, ShortcutRouter.resolveKey (k "F" true true false))
    Assert.Equal(ShortcutAction.OpenSettings, ShortcutRouter.resolveKey (k "OemComma" true false false))
    Assert.Equal(ShortcutAction.FocusComposer, ShortcutRouter.resolveKey (k "L" true false false))
    Assert.Equal(ShortcutAction.OpenModelPicker, ShortcutRouter.resolveKey (k "M" true false false))
    Assert.Equal(ShortcutAction.JumpExchange -1, ShortcutRouter.resolveKey (k "Up" true false false))
    Assert.Equal(ShortcutAction.JumpExchange 1, ShortcutRouter.resolveKey (k "Down" true false false))
    Assert.Equal(ShortcutAction.ScrollToBeginning, ShortcutRouter.resolveKey (k "Home" true false false))
    Assert.Equal(ShortcutAction.ScrollToEnd, ShortcutRouter.resolveKey (k "End" true false false))
    Assert.Equal(ShortcutAction.ToggleTheme, ShortcutRouter.resolveKey (k "S" true true false))
    Assert.Equal(ShortcutAction.ExportConversation, ShortcutRouter.resolveKey (k "E" true true false))
    Assert.Equal(ShortcutAction.CopyLastReply, ShortcutRouter.resolveKey (k "C" true true false))
    Assert.Equal(ShortcutAction.RegenerateLast, ShortcutRouter.resolveKey (k "G" true true false))
    Assert.Equal(ShortcutAction.UndoPending, ShortcutRouter.resolveKey (k "Z" true false false))
    Assert.Equal(ShortcutAction.OpenConversationAt 0, ShortcutRouter.resolveKey (k "D1" true false false))
    Assert.Equal(ShortcutAction.OpenConversationAt 8, ShortcutRouter.resolveKey (k "D9" true false false))
    Assert.Equal(ShortcutAction.ShowShortcuts, ShortcutRouter.resolveKey (k "OemQuestion" true false false))
    Assert.Equal(ShortcutAction.ShowShortcuts, ShortcutRouter.resolveKey (k "Divide" true false false))
    Assert.Equal(ShortcutAction.ShowShortcuts, ShortcutRouter.resolveKey (k "F1" false false false))
    Assert.Equal(ShortcutAction.FocusToast, ShortcutRouter.resolveKey (k "F6" false false true))
    Assert.Equal(ShortcutAction.NoShortcut, ShortcutRouter.resolveKey (k "A" false false true))
