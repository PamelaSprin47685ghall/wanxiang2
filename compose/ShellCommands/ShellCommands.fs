namespace Wanxiang.UI

open System

[<RequireQualifiedAccess>]
type ShellTone =
    | Info
    | Warning

/// 壳层命令要读的事实与要触达的界面面。都在宿主（浮层、设置页、侧栏、对话视图、输入区）。
type ShellSurface = {
    // ---- 事实 ----
    /// 浮层自己先接 Escape（overlay.HandleEscape），返回是否已处理。
    OverlayEscape: unit -> bool
    IsDialogOpen: unit -> bool
    IsPopupOpen: unit -> bool
    IsSettingsOpen: unit -> bool
    /// 窄屏且抽屉开着。
    IsDrawerOpen: unit -> bool
    /// 当前对话正在生成。
    IsGenerating: unit -> bool
    /// 有打开的对话（导出用）。
    HasConversation: unit -> bool
    /// 侧栏当前可见顺序（Ctrl+1..9）。
    VisibleOrder: unit -> string list
    /// 这个预设还在不在。
    HasPreset: string -> bool
    PaletteInput: unit -> ShellPaletteInput
    Theme: unit -> ThemePreference
    // ---- 界面面 ----
    CloseSettings: unit -> unit
    CloseDrawer: unit -> unit
    StopGeneration: unit -> unit
    ToggleSidebar: unit -> unit
    /// 关窗；没有 Window（PWA）时返回 false。
    CloseWindow: unit -> bool
    NewConversation: unit -> unit
    FocusSearch: string option -> unit
    /// 打开设置页；带分区键时跳到该分区（找不到分区只打开设置页）。
    OpenSettings: string option -> unit
    OpenModelPicker: unit -> unit
    JumpExchange: int -> unit
    ScrollToBeginning: unit -> unit
    ScrollToEnd: unit -> unit
    FocusComposer: unit -> unit
    CopyLastReply: unit -> unit
    RegenerateLast: unit -> unit
    /// 决策 232：撤销提示条可见、焦点不在有字的输入框时点「撤销」，返回是否接管。
    TryUndo: unit -> bool
    FocusToast: unit -> bool
    ExportConversation: unit -> unit
    OpenConversation: Guid -> unit
    ShowShortcuts: unit -> unit
    ApplyPreset: string -> unit
    SavePreset: unit -> unit
    Fork: unit -> unit
    Import: unit -> unit
    AddProvider: unit -> unit
    ShowPalette: (unit -> PaletteItem list) -> (unit -> string list) -> unit
    SaveRecent: string list -> unit
    SaveTheme: ThemePreference -> unit
    Toast: string -> ShellTone -> unit
}

/// 旧 AppShell 的快捷键、命令面板分派、主题轮转。拥有命令面板的最近使用清单。
type ShellCommands(surface: ShellSurface, initialRecent: string list) =
    let mutable recent = initialRecent

    member _.Recent = recent

    /// 主题三态轮转：跟随系统 → 浅色 → 深色 → 跟随系统。
    member _.CycleTheme() =
        let nextTheme = ThemePreference.next (surface.Theme())
        surface.SaveTheme nextTheme
        surface.Toast (ThemePreference.switchedText nextTheme) ShellTone.Info

    member private _.Export() =
        if surface.HasConversation() then surface.ExportConversation()
        else surface.Toast "没有打开的对话可导出" ShellTone.Warning

    member _.ShowPalette() =
        surface.ShowPalette (fun () -> ShellPalette.items (surface.PaletteInput())) (fun () -> recent)

    member this.RunPaletteItem(item: PaletteItem, query: string) =
        recent <- CommandPalette.pushRecent item.id recent
        surface.SaveRecent recent
        let command = ShellPalette.commandOf item.id
        // 预设已不存在：只提示，不收抽屉（旧分派先查预设）。
        let missingPreset =
            match command with
            | PaletteCommand.ApplyPreset id -> not (surface.HasPreset id)
            | _ -> false
        if ShellPalette.closesDrawer command && not missingPreset && surface.IsDrawerOpen() then surface.CloseDrawer()
        match command with
        | PaletteCommand.SearchMessages -> surface.FocusSearch(Some(query.Trim()))
        | PaletteCommand.OpenConversation id -> surface.OpenConversation id
        | PaletteCommand.OpenSettings key -> surface.OpenSettings(Some key)
        | PaletteCommand.ApplyPreset _ when missingPreset -> surface.Toast "这个预设已经不存在。" ShellTone.Warning
        | PaletteCommand.ApplyPreset id -> surface.ApplyPreset id
        | PaletteCommand.Action "new" -> surface.NewConversation()
        | PaletteCommand.Action "save-preset" -> surface.SavePreset()
        | PaletteCommand.Action "model" -> surface.OpenModelPicker()
        | PaletteCommand.Action "export" -> this.Export()
        | PaletteCommand.Action "fork" -> surface.Fork()
        | PaletteCommand.Action "copy-reply" -> surface.CopyLastReply()
        | PaletteCommand.Action "regenerate" -> surface.RegenerateLast()
        | PaletteCommand.Action "import" -> surface.Import()
        | PaletteCommand.Action "theme" -> this.CycleTheme()
        | PaletteCommand.Action "sidebar" -> surface.ToggleSidebar()
        | PaletteCommand.Action "shortcuts" -> surface.ShowShortcuts()
        | PaletteCommand.Action "settings" -> surface.OpenSettings None
        | PaletteCommand.Action "add-provider" -> surface.AddProvider()
        | PaletteCommand.Action _ | PaletteCommand.Ignore -> ()

    /// 旧 HandleShortcut：返回是否接管（宿主据此设 e.Handled）。
    member this.HandleShortcut(shortcut: ShortcutAction) : bool =
        match shortcut with
        | ShortcutAction.Escape ->
            // 浮层先试；落到这里说明焦点在抽屉外（D3/D4）。
            let overlayHandled = surface.OverlayEscape()
            match EscapeChain.decide overlayHandled (surface.IsSettingsOpen()) (surface.IsDrawerOpen()) (surface.IsGenerating()) with
            | EscapeTarget.Overlay -> true
            | EscapeTarget.CloseSettings -> surface.CloseSettings(); true
            | EscapeTarget.CloseDrawer -> surface.CloseDrawer(); true
            | EscapeTarget.StopGeneration -> surface.StopGeneration(); true
            | EscapeTarget.Unhandled -> false
        | ShortcutAction.NoShortcut -> false
        | _ when EscapeChain.blockedByOverlay (surface.IsDialogOpen()) (surface.IsPopupOpen()) -> false
        | ShortcutAction.ToggleSidebar -> surface.ToggleSidebar(); true
        | ShortcutAction.CloseWindow -> surface.CloseWindow()
        | ShortcutAction.NewConversation -> surface.NewConversation(); true
        | ShortcutAction.FocusSearch -> surface.FocusSearch None; true
        | ShortcutAction.OpenSettings -> surface.OpenSettings None; true
        | ShortcutAction.OpenModelPicker -> surface.OpenModelPicker(); true
        | ShortcutAction.JumpExchange direction -> surface.JumpExchange direction; true
        | ShortcutAction.ScrollToBeginning -> surface.ScrollToBeginning(); true
        | ShortcutAction.ScrollToEnd -> surface.ScrollToEnd(); true
        | ShortcutAction.FocusComposer -> surface.FocusComposer(); true
        | ShortcutAction.ToggleTheme -> this.CycleTheme(); true
        | ShortcutAction.OpenCommandPalette -> this.ShowPalette(); true
        | ShortcutAction.CopyLastReply -> surface.CopyLastReply(); true
        | ShortcutAction.RegenerateLast -> surface.RegenerateLast(); true
        | ShortcutAction.UndoPending -> surface.TryUndo()
        | ShortcutAction.FocusToast -> surface.FocusToast()
        | ShortcutAction.ExportConversation -> this.Export(); true
        | ShortcutAction.OpenConversationAt index ->
            // Ctrl+1..9 按侧栏可见顺序（C2）。
            match EscapeChain.conversationAt index (surface.VisibleOrder()) with
            | Some id -> surface.OpenConversation id; true
            | None -> false
        | ShortcutAction.ShowShortcuts -> surface.ShowShortcuts(); true
