namespace Wanxiang.Client

open System
open System.Net.Sockets

/// 连接失败的**类型化**原因（W2，P11）。
///
/// 界面按这里的类型决定“要不要给重试按钮、按钮做什么”，而不是靠错误文案里是否含
/// “失败 / 拒绝 / 超时”之类关键词——文案一改行为就变，是实现隐患。
[<RequireQualifiedAccess>]
type ConnectionIssue =
    /// 对端拒绝了连接（端口没人监听、主动拒绝）。
    | Rejected of detail: string
    /// 在限定时间内没有建立连接或没有收到回应。
    | TimedOut
    /// 令牌无效或已被吊销：重试同一个令牌没有意义，要重新配对。
    | TokenInvalid
    /// 配对失败次数过多，该地址被冻结 `minutes` 分钟。
    | PairingFrozen of minutes: int
    /// 配对码错误、已用或已过期（还没被冻结）。
    | PairingRejected of reason: string
    /// 其他断线（网络中断、主机关闭等）。
    | Disconnected of detail: string

/// 界面能提供的“下一步”。
type ConnectionRetry =
    /// 原样再连一次。
    | RetryConnect
    /// 回到配对流程（重新请求配对码）。
    | RePair
    /// 再试一次配对（码错了就重新输入）。
    | RetryPairing

module ConnectionIssue =

    /// 每种类型对应的重试动作；None = 不给按钮（重试只会继续失败）。
    let retry (issue: ConnectionIssue) : ConnectionRetry option =
        match issue with
        | ConnectionIssue.Rejected _ -> Some RetryConnect
        | ConnectionIssue.TimedOut -> Some RetryConnect
        | ConnectionIssue.Disconnected _ -> Some RetryConnect
        | ConnectionIssue.TokenInvalid -> Some RePair
        | ConnectionIssue.PairingRejected _ -> Some RetryPairing
        | ConnectionIssue.PairingFrozen _ -> None

    /// 由连接异常归类：沿着 InnerException 找到最有信息量的一层。
    let ofException (ex: exn) : ConnectionIssue =
        let rec chain (e: exn) =
            seq {
                if not (isNull e) then
                    yield e
                    yield! chain e.InnerException
            }

        let all = chain ex |> Seq.toList

        let socketError =
            all
            |> List.tryPick (function
                | :? SocketException as s -> Some s.SocketErrorCode
                | _ -> None)

        match socketError with
        | Some SocketError.ConnectionRefused -> ConnectionIssue.Rejected ex.Message
        | Some SocketError.TimedOut -> ConnectionIssue.TimedOut
        | _ ->
            if
                all
                |> List.exists (fun e -> e :? TimeoutException || e :? OperationCanceledException)
            then
                ConnectionIssue.TimedOut
            else
                ConnectionIssue.Disconnected ex.Message

    /// 主机报来的配对失败：被冻结时带分钟数，否则带原因（迁自旧 AppShell 的 PairingFailed 分支）。
    let ofPairingFailure (frozen: bool) (freezeMinutes: int) (reason: string) : ConnectionIssue =
        if frozen then
            ConnectionIssue.PairingFrozen freezeMinutes
        else
            ConnectionIssue.PairingRejected reason

    /// 连接对话框的状态行：配对码已发出、配对成功正在认证。
    let pairingStartedStatus = "配对码已生成，请输入后提交。"
    let pairingSucceededStatus = "配对成功，正在认证…"
