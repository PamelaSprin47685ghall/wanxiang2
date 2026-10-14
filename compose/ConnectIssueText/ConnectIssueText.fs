namespace Wanxiang.UI

open System
open Wanxiang.Client

/// 连接对话框的错误文案（旧 Views/Dialogs.fs 的 Dialogs.describeIssue 与 Dialogs.retryLabel）。
module ConnectIssueText =

    /// 类型化连接错误的用户可见文案：发生了什么 / 为什么 / 下一步。
    /// 只用于展示；是否出现重试按钮看 `ConnectionIssue.retry`，不看这里的字。
    let describe (issue: ConnectionIssue) : string =
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
