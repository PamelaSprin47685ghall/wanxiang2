namespace Wanxiang.UI

open System
open System.IO

/// 本机界面偏好的落点（ui.json）。解析、渲染与主题判断在 Wanxiang2.UiPrefs；
/// 这里只给出取值边界、读写文件并收紧权限（IO 壳迁入 wanxiang2）。
module UiPrefsFile =

    /// 取值边界的唯一事实来源在设计层。数值与宿主 Tokens.sidebarWidth/sidebarMinWidth/sidebarMaxWidth
    /// 及 LayoutPolicy.desktopMinWidth 一致（wanxiang2 侧同源：ThemeMetrics / DesignRhythm）。
    let limits: UiPrefLimits =
        {
            sidebarWidth = 284.0
            sidebarMinWidth = 232.0
            sidebarMaxWidth = 420.0
            windowMinWidth = 700.0
        }

    let defaults = UiPrefs.defaults limits

    let filePath () =
        Path.Combine(LocalState.configHome (), "ui.json")

    let private readText () =
        try
            let p = filePath ()
            if File.Exists p then Some(File.ReadAllText p) else None
        with _ ->
            None

    let load () : UiPrefs = UiPrefs.parse limits (readText ())

    let save (prefs: UiPrefs) =
        try
            let path = filePath ()
            let text = UiPrefs.render (readText ()) prefs
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
            let tmp = path + ".tmp"
            File.WriteAllText(tmp, text)
            File.Move(tmp, path, true)

            if not (OperatingSystem.IsBrowser()) then
                // Q118：本机偏好文件按最小用户权限落盘（与 TOML/日志/附件一致）
                try
                    File.SetUnixFileMode(path, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
                with _ ->
                    // Windows 或不支持 POSIX 权限的文件系统忽略即可
                    ()
        with ex ->
            // 路径位于 Git 边界内被拒或磁盘不可写时退化为不落盘，仅记后台诊断（LEMMA 3）
            eprintfn "[wanxiang] 保存界面偏好 ui.json 失败：%s" ex.Message

    /// 偏好 → 是否深色。浏览器与桌面都由调用方给出系统是否深色；
    /// 返回 bool 而非宿主设计层的 ThemeMode（wanxiang2 无 ThemeMode，主题模式归 ThemePalette 的 PaletteMode），
    /// 由调用方映射成自己的主题模式。
    let resolveTheme (prefs: UiPrefs) (systemIsDark: bool) : bool = UiPrefs.isDark prefs systemIsDark
