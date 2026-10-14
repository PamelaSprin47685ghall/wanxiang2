namespace Wanxiang2.KeyChord

open Avalonia.Input

/// 一次按键，与平台无关的形状（迁自旧 Controls/KeyShortcut.fs）。⌘ 与 Ctrl 同义。
/// 字段与 ShortcutKeys 库的 ShortcutKey 一一对应；两者的换算在组合者里做。
type KeyChord =
    { key: string
      ctrl: bool
      shift: bool
      noModifiers: bool }

module KeyChord =
    let ofKey (key: Key) (modifiers: KeyModifiers) : KeyChord =
        { key = string key
          ctrl = modifiers.HasFlag KeyModifiers.Control || modifiers.HasFlag KeyModifiers.Meta
          shift = modifiers.HasFlag KeyModifiers.Shift
          noModifiers = (modifiers = KeyModifiers.None) }

    let ofEvent (e: KeyEventArgs) : KeyChord = ofKey e.Key e.KeyModifiers
