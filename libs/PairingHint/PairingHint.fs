namespace Wanxiang.UI

open System

/// 连接与配对对话框里不碰控件的判断与提示（旧 Views/Dialogs.fs 的 ConnectDialogMode.pairingFirst、
/// Dialogs.isLoopbackUrl、Dialogs.pairingCodeHint）。
module PairingHint =

    /// 决策 260：连接对话框以哪种方式打开。
    /// 本机没有令牌、没有保存的凭据、也没有待说明的连接错误 → 首次连接，直接走配对码。
    let pairingFirst (token: string option) (hasSavedCredentials: bool) (connectionError: string option) : bool =
        token.IsNone && not hasSavedCredentials && connectionError.IsNone

    /// 地址是否指向本机（决定配对码提示该怎么拿）。
    let isLoopbackUrl (url: string) : bool =
        match Uri.TryCreate((if isNull url then "" else url.Trim()), UriKind.Absolute) with
        | true, uri -> uri.IsLoopback || uri.Host = "localhost"
        | _ -> false

    /// 配对码从哪里拿：本机与远端不同。
    let codeHint (url: string) : string =
        if isLoopbackUrl url then
            "在运行万象的这台机器上：打开桌面应用的「设置 → 设备 → 添加设备」，或查看启动万象的终端输出。5 分钟内有效。"
        else
            "这是远端主机：请在已连接的桌面应用里打开「设置 → 设备 → 添加设备」，会显示 6 位配对码。主机没有桌面应用时，查看它的终端输出。5 分钟内有效。"
