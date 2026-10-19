namespace Wanxiang2.WorkspaceState

open System

/// 「加载更早消息」的超时与失败提示。
module OlderPageLoad =

    /// 是否有请求在途（正在加载，或已记下发出时间）。只有在途时失败才需要呈现。
    let inFlight (loading: bool) (requestedAt: DateTimeOffset option) = loading || requestedAt.IsSome

    /// 是否已超时：正在加载、记下了发出时间，且已等满 timeout。
    let timedOut (loading: bool) (requestedAt: DateTimeOffset option) (now: DateTimeOffset) (timeout: TimeSpan) =
        match requestedAt with
        | Some at when loading && now - at >= timeout -> true
        | _ -> false

    let timeoutText = "加载更早消息超时，主机没有回应。可以点「重试」。"

    /// 点「重试」时还没连上主机。
    let offlineText = "还没有连接主机，更早的消息无法加载。连接后再点「重试」。"
