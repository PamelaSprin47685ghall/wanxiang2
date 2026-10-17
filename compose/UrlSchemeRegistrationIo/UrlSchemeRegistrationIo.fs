namespace Wanxiang.UI

open System
open System.IO

/// 决策 251：系统注册 `wanxiang://` 协议。只写当前用户范围，幂等（内容不变就不碰）。
/// 纯文本部分（desktopFileName/desktopEntry/quote）在 Wanxiang2.UrlSchemeRegistration；
/// 这里补写桌面文件、写 Windows 注册表、求当前进程启动命令（launchCommand / registerCurrentUser）。
/// 组合者：libs/UrlSchemeRegistration/UrlSchemeRegistration.fsproj 已有纯文本部分，本文件经交付包引用它。
module UrlSchemeRegistration =

    /// 当前进程的启动命令：apphost 直接用它；`dotnet wanxiang.dll` 时带上程序集。
    let launchCommand () : string option =
        match Environment.ProcessPath with
        | null -> None
        | exe ->
            let name = Path.GetFileNameWithoutExtension exe
            if name.Equals("dotnet", StringComparison.OrdinalIgnoreCase) then
                match Reflection.Assembly.GetEntryAssembly() with
                | null -> None
                | asm when not (String.IsNullOrEmpty asm.Location) -> Some(UrlSchemeRegistration.quote exe + " " + UrlSchemeRegistration.quote asm.Location)
                | _ -> None
            else Some(UrlSchemeRegistration.quote exe)

    let private runQuiet (file: string) (args: string list) =
        try
            let psi = Diagnostics.ProcessStartInfo(file)
            for a in args do psi.ArgumentList.Add a
            psi.UseShellExecute <- false
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            use p = Diagnostics.Process.Start psi
            p.WaitForExit 3000 |> ignore
        with _ -> ()

    let private registerLinux (command: string) =
        let dataHome =
            match Environment.GetEnvironmentVariable "XDG_DATA_HOME" with
            | s when not (String.IsNullOrWhiteSpace s) -> s
            | _ -> Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".local", "share")
        let dir = Path.Combine(dataHome, "applications")
        let path = Path.Combine(dir, UrlSchemeRegistration.desktopFileName)
        let content = UrlSchemeRegistration.desktopEntry command
        let existing = if File.Exists path then (try File.ReadAllText path with _ -> "") else ""
        if existing <> content then
            Directory.CreateDirectory dir |> ignore
            File.WriteAllText(path, content)
            runQuiet "xdg-mime" [ "default"; UrlSchemeRegistration.desktopFileName; "x-scheme-handler/wanxiang" ]
            runQuiet "update-desktop-database" [ dir ]

    let private registerWindows (command: string) =
        if OperatingSystem.IsWindows() then
            use key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\wanxiang")
            key.SetValue("", "URL:万象消息链接")
            key.SetValue("URL Protocol", "")
            use cmd = key.CreateSubKey(@"shell\open\command")
            let wanted = command + " \"%1\""
            if (cmd.GetValue("") :?> string) <> wanted then cmd.SetValue("", wanted)

    /// 尽力而为：失败只记日志，不影响启动。macOS 由 .app 的 Info.plist（CFBundleURLTypes）声明，
    /// 运行时无需注册；系统通过 IActivatableLifetime 的 OpenUri 激活把链接交进来。
    /// `WANXIANG_NO_URL_SCHEME=1` 关闭（开发 / 测试环境不改写用户的协议关联）。
    let registerCurrentUser (log: string -> unit) =
        if Environment.GetEnvironmentVariable "WANXIANG_NO_URL_SCHEME" <> "1" then
            match launchCommand () with
            | None -> ()
            | Some command ->
                try
                    if OperatingSystem.IsLinux() then registerLinux command
                    elif OperatingSystem.IsWindows() then registerWindows command
                with e -> log (sprintf "url scheme registration skipped: %s" e.Message)
