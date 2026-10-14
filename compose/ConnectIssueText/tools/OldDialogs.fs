namespace Wanxiang.UI

open System
open Wanxiang.Client

/// 决策 260：连接对话框以哪种方式打开。
module ConnectDialogMode =
    /// 本机没有令牌、没有保存的凭据、也没有待说明的连接错误 → 首次连接，直接走配对码。
    let pairingFirst (token: string option) (hasSavedCredentials: bool) (connectionError: string option) : bool =
        token.IsNone && not hasSavedCredentials && connectionError.IsNone


module Dialogs =
    /// 类型化连接错误的用户可见文案：发生了什么 / 为什么 / 下一步。
    /// 只用于展示；是否出现重试按钮看 `ConnectionIssue.retry`，不看这里的字。
    let describeIssue (issue: ConnectionIssue) : string =
        match issue with
        | ConnectionIssue.Rejected _ -> "主机拒绝了连接。可能没有在运行，或地址、端口不对。检查地址后再试。"
        | ConnectionIssue.TimedOut -> "连接超时。主机没有及时回应，可能网络不通或主机在休眠。确认主机在线后再试。"
        | ConnectionIssue.TokenInvalid -> "这台设备的授权已失效。令牌无效或已被吊销。请重新配对。"
        | ConnectionIssue.PairingFrozen minutes ->
            sprintf "配对已冻结 %d 分钟。连续输错次数过多。稍后再试，或请已连接的桌面端为这台设备发配对码。" minutes
        | ConnectionIssue.PairingRejected reason ->
            let why =
                match reason with
                | "invalid pairing code" -> "配对码不对"
                | "pairing code expired" -> "配对码已过期"
                | "pairing code already used" -> "配对码已被使用"
                | "no pairing session active" -> "没有进行中的配对"
                | other -> other
            sprintf "配对没有成功：%s。请核对 6 位配对码后再试一次。" why
        | ConnectionIssue.Disconnected detail ->
            if String.IsNullOrWhiteSpace detail then "连接已断开。网络中断或主机已关闭。检查后重新连接。"
            else sprintf "连接已断开：%s。网络中断或主机已关闭。检查后重新连接。" detail

    /// 重试按钮的文案（只对应动作，不对应错误文案）。
    let retryLabel (retry: ConnectionRetry) : string =
        match retry with
        | RetryConnect -> "重试连接"
        | RePair -> "重新配对"
        | RetryPairing -> "重试配对"

    /// 地址是否指向本机（决定配对码提示该怎么拿）。
    let isLoopbackUrl (url: string) : bool =
        match Uri.TryCreate((if isNull url then "" else url.Trim()), UriKind.Absolute) with
        | true, uri ->
            uri.IsLoopback || uri.Host = "localhost"
        | _ -> false

    /// 配对码从哪里拿：本机与远端不同。
    let pairingCodeHint (url: string) : string =
        if isLoopbackUrl url then
            "在运行万象的这台机器上：打开桌面应用的「设置 → 设备 → 添加设备」，或查看启动万象的终端输出。5 分钟内有效。"
        else
            "这是远端主机：请在已连接的桌面应用里打开「设置 → 设备 → 添加设备」，会显示 6 位配对码。主机没有桌面应用时，查看它的终端输出。5 分钟内有效。"
