namespace Wanxiang.UI

/// 决策 251：`wanxiang://` 链接的系统注册所需的纯文本部分。
/// 写文件、改注册表、找当前进程命令仍归宿主（旧 `DesktopLinks.fs` 其余部分）；同名模块合并，旧调用方不用改。
module UrlSchemeRegistration =

    let desktopFileName = "wanxiang-url-handler.desktop"

    /// Linux 桌面文件：`%u` 把链接交给万象；NoDisplay 不在应用菜单里多出一个图标。
    let desktopEntry (command: string) =
        String.concat "\n"
            [ "[Desktop Entry]"
              "Type=Application"
              "Name=万象"
              "Comment=打开万象消息链接"
              sprintf "Exec=%s %%u" command
              "Terminal=false"
              "NoDisplay=true"
              "MimeType=x-scheme-handler/wanxiang;"
              "" ]

    /// 引号包住含空格的路径（桌面文件与注册表命令都用）。
    let quote (path: string) = if path.Contains ' ' then "\"" + path + "\"" else path
