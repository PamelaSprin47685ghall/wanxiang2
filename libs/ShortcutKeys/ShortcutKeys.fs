namespace Wanxiang.UI

open System

/// 全局快捷键只做“按键 → 意图”解析；是否允许在 modal 打开时执行、
/// 以及具体副作用由 MainView 决定。
[<RequireQualifiedAccess>]
type ShortcutAction =
    | Escape
    | ToggleSidebar
    | NewConversation
    | FocusSearch
    /// 命令面板（决策 370/372）：Ctrl/⌘+K。侧栏搜索改到 Ctrl/⌘+Shift+F。
    | OpenCommandPalette
    | OpenSettings
    | ToggleTheme
    | ExportConversation
    | OpenConversationAt of int
    | ShowShortcuts
    /// 打开当前对话的模型选择器（输入框内可直接换模型，不必先点芯片）。
    | OpenModelPicker
    /// 聚焦消息输入框：滚回长对话历史后想继续打字，不必先拿鼠标点一下输入框。
    | FocusComposer
    /// 跳到上一条 / 下一条用户提问：长对话里找上一个提问不必拖滚轮。
    | JumpExchange of int
    /// 回到对话顶部 / 底部：长对话此前只有「回到最新」一条路，想看最早那个提问
    /// 只能一路拖滚轮，还没有锚点。端点两侧对称可达，翻回开头才有落脚处。
    | ScrollToBeginning
    | ScrollToEnd
    /// 关闭主窗口：桌面端 Ctrl+W 的通用语义，与 kelivo 的 close_window 同键。
    | CloseWindow
    /// 复制最新一条回复（与 ChatGPT 同键）：长回复读完想带走，不必先找卡片菜单（决策 220）。
    | CopyLastReply
    /// 重新生成最新回复。不用 Ctrl+Shift+R：浏览器里那是强制刷新，PWA 会丢掉页面状态（决策 220）。
    | RegenerateLast
    /// 撤销提示条里待生效的动作（删除宽限期内的「撤销」，决策 232）。
    | UndoPending
    /// 焦点移到最新一条提示条的动作按钮（决策 311）。
    | FocusToast
    | NoShortcut

/// 一次按键的平台无关描述。宿主把自己的按键事件映射过来：
/// key 是按键名（与 Avalonia `Key` 枚举名相同，如 "B"、"OemComma"、"D1"、"Up"）；
/// ctrl 含 macOS 的 ⌘；noModifiers 表示没有按任何修饰键。
type ShortcutKey = {
    key: string
    ctrl: bool
    shift: bool
    noModifiers: bool
}

module ShortcutRouter =

    let private digits = [| "D1"; "D2"; "D3"; "D4"; "D5"; "D6"; "D7"; "D8"; "D9" |]

    /// 按键 → 意图。
    let resolveKey (k: ShortcutKey) : ShortcutAction =
        let ctrl, shift, key = k.ctrl, k.shift, k.key
        if key = "Escape" then ShortcutAction.Escape
        elif ctrl && key = "B" then ShortcutAction.ToggleSidebar
        // 与指针点右上角叉号同一个动作：桌面端关窗只有一条路，键盘用户也得有。
        elif ctrl && not shift && key = "W" then ShortcutAction.CloseWindow
        elif ctrl && key = "N" then ShortcutAction.NewConversation
        elif ctrl && key = "K" then ShortcutAction.OpenCommandPalette
        elif ctrl && shift && key = "F" then ShortcutAction.FocusSearch
        elif ctrl && key = "OemComma" then ShortcutAction.OpenSettings
        elif ctrl && key = "L" then ShortcutAction.FocusComposer
        elif ctrl && not shift && key = "M" then ShortcutAction.OpenModelPicker
        // 与顶栏两个跳转按钮完全同一动作；输入框里也能直接翻，不必先移开焦点。
        elif ctrl && not shift && key = "Up" then ShortcutAction.JumpExchange -1
        elif ctrl && not shift && key = "Down" then ShortcutAction.JumpExchange 1
        elif ctrl && not shift && key = "Home" then ShortcutAction.ScrollToBeginning
        elif ctrl && not shift && key = "End" then ShortcutAction.ScrollToEnd
        elif ctrl && shift && key = "S" then ShortcutAction.ToggleTheme
        elif ctrl && shift && key = "E" then ShortcutAction.ExportConversation
        elif ctrl && shift && key = "C" then ShortcutAction.CopyLastReply
        elif ctrl && shift && key = "G" then ShortcutAction.RegenerateLast
        elif ctrl && not shift && key = "Z" then ShortcutAction.UndoPending
        elif ctrl && not shift && Array.contains key digits then
            ShortcutAction.OpenConversationAt(Array.IndexOf(digits, key))
        elif ctrl && (key = "OemQuestion" || key = "Divide") then ShortcutAction.ShowShortcuts
        elif key = "F1" then ShortcutAction.ShowShortcuts
        elif key = "F6" && k.noModifiers then ShortcutAction.FocusToast
        else ShortcutAction.NoShortcut
