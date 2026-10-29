namespace Wanxiang.UI

open Wanxiang.Core

/// 末尾用户消息久无回复时补出的失败卡（决策 263，迁自旧 src/Wanxiang.UI/Model/UnansweredReply.fs）。
/// 文案与判断在 Wanxiang2.WorkspaceState 的 ReplyTail（unansweredMessage / unansweredDetail / isUnanswered）；
/// 这里只拼出失败卡的 GenerationError 值（形状归 Wanxiang2.Wire）。
/// 装配缺口：旧 src 全量检索 UnansweredReply 仅命中定义处（无调用方），wx2 的 AppShell / ChatScreen
/// 也没有 isUnanswered 消费缝；渲染装配点（谁在何时检查 isUnanswered 并补卡）需另行裁决。
module UnansweredReply =

    let error: GenerationError =
        { GenerationError.create GenerationErrorKind.UnknownFailure ReplyTail.unansweredMessage with
            detail = Some ReplyTail.unansweredDetail
        }
