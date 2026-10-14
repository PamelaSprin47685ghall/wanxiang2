namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 主题偏好。`System` 由宿主解析成明/暗。
type ThemePreference =
    | FollowSystem
    | AlwaysLight
    | AlwaysDark

module ThemePreference =

    let toText (p: ThemePreference) =
        match p with
        | FollowSystem -> "system"
        | AlwaysLight -> "light"
        | AlwaysDark -> "dark"

    let ofText (s: string) =
        match s with
        | "light" -> AlwaysLight
        | "dark" -> AlwaysDark
        | _ -> FollowSystem

    let label (p: ThemePreference) =
        match p with
        | FollowSystem -> "跟随系统"
        | AlwaysLight -> "浅色"
        | AlwaysDark -> "深色"

/// 本机 UI 偏好（`$WANXIANG_HOME/ui.json` 的内容；Q195：不写业务 TOML、不经 NDJSON）。
/// 本库只管文本 ⇄ 偏好，读写文件由宿主做。
type UiPrefs = {
    theme: ThemePreference
    /// 消息正文字号
    fontScale: float
    /// 完成生成后自动收起思考过程
    autoCollapseReasoning: bool
    /// 代码块长行折行显示（false = 横向滚动）
    codeWrap: bool
    /// 侧栏宽度
    sidebarWidth: float
    /// 侧栏是否折叠
    sidebarCollapsed: bool
    /// 是否展示归档对话
    showArchived: bool
    /// 减少非必要动态效果
    reduceMotion: bool
    /// 发送键：true = Enter 发送，false = Ctrl+Enter 发送
    enterSends: bool
    /// 在本机保存未发送的草稿与未确认的消息，重新打开后恢复（W5，决策 80）。
    /// 默认开启；只存本机、不上传、不同步；关闭后立即删除已存草稿。
    persistDrafts: bool
    /// 窗口几何（桌面端）
    windowWidth: float
    windowHeight: float
    windowX: int
    windowY: int
    hasWindowPosition: bool
}

/// 取值边界。数值的唯一事实来源在设计层（侧栏宽度、桌面最小宽），由宿主传入。
type UiPrefLimits = {
    sidebarWidth: float
    sidebarMinWidth: float
    sidebarMaxWidth: float
    /// 主窗口最小宽（C7：与 LayoutPolicy.desktopMinWidth 同源）
    windowMinWidth: float
}

module UiPrefs =

    let minFontScale = 12.0
    let maxFontScale = 19.0

    let defaults (limits: UiPrefLimits) =
        { theme = FollowSystem
          fontScale = 14.5
          autoCollapseReasoning = true
          codeWrap = false
          sidebarWidth = limits.sidebarWidth
          sidebarCollapsed = false
          showArchived = false
          reduceMotion = false
          enterSends = true
          persistDrafts = true
          windowWidth = 1240.0
          windowHeight = 800.0
          windowX = 0
          windowY = 0
          hasWindowPosition = false }

    /// 已存文本读成 JSON 对象；没有、损坏或不是对象都当空对象。
    let private objectOf (text: string option) =
        try
            match text with
            | Some text ->
                match JsonNode.Parse text with
                | null -> JsonObject()
                | node -> node.AsObject()
            | None -> JsonObject()
        with _ ->
            JsonObject()

    let private clamp lo hi value = if value < lo then lo elif value > hi then hi else value

    /// 已存文本 → 偏好。任何读错整份回落默认值，绝不抛出。
    let parse (limits: UiPrefLimits) (text: string option) : UiPrefs =
        let defaults = defaults limits
        try
            let o = objectOf text
            let boolOf key fallback =
                let mutable n: JsonNode = null
                if o.TryGetPropertyValue(key, &n) && not (isNull n) then
                    match n.GetValueKind() with
                    | JsonValueKind.True -> true
                    | JsonValueKind.False -> false
                    | _ -> fallback
                else
                    fallback
            let floatOf key fallback =
                let mutable n: JsonNode = null
                if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
                    let v = n.GetValue<double>()
                    if Double.IsNaN v then fallback else v
                else
                    fallback
            let intOf key fallback =
                let mutable n: JsonNode = null
                if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
                    n.GetValue<int>()
                else
                    fallback
            let stringOf key fallback =
                let mutable n: JsonNode = null
                if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.String then
                    n.GetValue<string>()
                else
                    fallback
            { theme = ThemePreference.ofText(stringOf "theme" "system")
              fontScale = floatOf "fontScale" defaults.fontScale |> clamp minFontScale maxFontScale
              autoCollapseReasoning = boolOf "autoCollapseReasoning" defaults.autoCollapseReasoning
              codeWrap = boolOf "codeWrap" defaults.codeWrap
              sidebarWidth = floatOf "sidebarWidth" defaults.sidebarWidth |> clamp limits.sidebarMinWidth limits.sidebarMaxWidth
              sidebarCollapsed = boolOf "sidebarCollapsed" defaults.sidebarCollapsed
              showArchived = boolOf "showArchived" defaults.showArchived
              reduceMotion = boolOf "reduceMotion" defaults.reduceMotion
              enterSends = boolOf "enterSends" defaults.enterSends
              persistDrafts = boolOf "persistDrafts" defaults.persistDrafts
              // 桌面最小宽与主窗口下限同源（C7），由 limits 传入。
              windowWidth = floatOf "windowWidth" defaults.windowWidth |> clamp limits.windowMinWidth 6000.0
              windowHeight = floatOf "windowHeight" defaults.windowHeight |> clamp 600.0 4000.0
              windowX = intOf "windowX" 0
              windowY = intOf "windowY" 0
              hasWindowPosition = boolOf "hasWindowPosition" false }
        with _ ->
            defaults

    /// 偏好 → 要写回的文本：保留已存文本里本程序不认识的键，只覆盖自己的键。
    let render (existing: string option) (prefs: UiPrefs) : string =
        let o = objectOf existing
        o["theme"] <- ThemePreference.toText prefs.theme
        o["fontScale"] <- prefs.fontScale
        o["autoCollapseReasoning"] <- prefs.autoCollapseReasoning
        o["codeWrap"] <- prefs.codeWrap
        o["sidebarWidth"] <- prefs.sidebarWidth
        o["sidebarCollapsed"] <- prefs.sidebarCollapsed
        o["showArchived"] <- prefs.showArchived
        o["reduceMotion"] <- prefs.reduceMotion
        o["enterSends"] <- prefs.enterSends
        o["persistDrafts"] <- prefs.persistDrafts
        o["windowWidth"] <- prefs.windowWidth
        o["windowHeight"] <- prefs.windowHeight
        o["windowX"] <- prefs.windowX
        o["windowY"] <- prefs.windowY
        o["hasWindowPosition"] <- prefs.hasWindowPosition
        o.ToJsonString(JsonSerializerOptions(WriteIndented = true))

    /// 偏好 → 是否用深色。系统是否深色由宿主给出。
    let isDark (prefs: UiPrefs) (systemIsDark: bool) : bool =
        match prefs.theme with
        | AlwaysLight -> false
        | AlwaysDark -> true
        | FollowSystem -> systemIsDark
