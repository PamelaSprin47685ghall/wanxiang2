/// 金样两边共用的消息样本：新旧两套渲染吃同一组数据（字段同名的 MessageView / GenerationUsage / GenerationError）
module Samples

open Wanxiang.Core
open Wanxiang.UI

/// 一张消息卡的上下文旗标（两边各自折成自己的上下文记录）
type Flags =
    {
        Streaming: bool
        IsLastAssistant: bool
        AutoCollapse: bool
        Usage: GenerationUsage option
        Missing: Set<string>
        Awaiting: Set<string>
        Index: int option
    }

let calm =
    {
        Streaming = false
        IsLastAssistant = false
        AutoCollapse = true
        Usage = None
        Missing = Set.empty
        Awaiting = Set.empty
        Index = Some 0
    }

let private msg role text =
    {
        role = role
        text = text
        reasoning = ""
        toolCalls = []
        toolResults = []
        attachments = []
        commitId = Some 7UL
        committedAt = None
    }

let private usage =
    {
        promptTokens = Some 1200
        completionTokens = Some 345
        cachedTokens = Some 800
        totalTokens = None
        durationMs = Some 2500L
    }

let private longText = String.replicate 40 "这是一段很长的用户输入，用来撑出限高视口。"

let private call id name args result =
    {
        callId = id
        name = name
        argumentsJson = args
        result = result
    }

/// 名称、消息、旗标
let all: (string * MessageView * Flags) list =
    [
        "user-short", msg "user" "你好，帮我看看这段代码。", calm
        "user-long", msg "user" longText, { calm with Index = Some 2 }
        "user-attach",
        { msg "user" "附件在这里" with
            attachments =
                [
                    {
                        sha256 = "a1"
                        size = 2048L
                        mediaType = "image/png"
                        fileName = "shot.png"
                    }
                    {
                        sha256 = "b2"
                        size = 5L * 1024L * 1024L
                        mediaType = "application/pdf"
                        fileName = "report.pdf"
                    }
                    {
                        sha256 = "c3"
                        size = 10L
                        mediaType = "text/plain"
                        fileName = "lost.txt"
                    }
                ]
        },
        { calm with
            Missing = Set.ofList [ "c3" ]
        }
        "assistant-plain", msg "assistant" "第一段。\n\n- 列表一\n- 列表二\n\n`code` 与 **强调**。", calm
        "assistant-last-usage",
        msg "assistant" "最后一条回复。",
        { calm with
            IsLastAssistant = true
            Usage = Some usage
            Index = Some 3
        }
        "assistant-reasoning-collapsed",
        { msg "assistant" "结论。" with
            reasoning = "先想一想。\n再想一想。"
        },
        { calm with Usage = Some usage }
        "assistant-reasoning-open",
        { msg "assistant" "结论。" with
            reasoning = "展开的思考。"
        },
        { calm with AutoCollapse = false }
        "assistant-tools",
        { msg "assistant" "工具跑完了。" with
            toolCalls =
                [
                    call "t1" "builtin_echo" "{\"text\":\"hi\"}" (Some "{\"echo\":\"hi\"}")
                    call "t2" "builtin_echo" "{\"text\":\"boom\"}" (Some "{\"error\":\"炸了\"}")
                    call "t3" "web_fetch" "" None
                ]
        },
        calm
        "assistant-tools-awaiting",
        { msg "assistant" "" with
            commitId = None
            toolCalls = [ call "t9" "builtin_echo" "{\"text\":\"wait\"}" None ]
        },
        { calm with
            Streaming = true
            Awaiting = Set.ofList [ "t9" ]
        }
        "assistant-streaming",
        { msg "assistant" "正在写" with
            commitId = None
        },
        { calm with
            Streaming = true
            IsLastAssistant = true
        }
    ]

/// 错误卡样本：名称、错误、有无「继续生成」
let errors: (string * GenerationError * bool) list =
    let e kind message detail retryable =
        {
            kind = kind
            message = message
            detail = detail
            retryable = retryable
            retryAfterSeconds = None
        }

    [
        "error-retry", e GenerationErrorKind.ProviderTimeout "模型服务超时" (Some "System.TimeoutException: 120s") true, false
        "error-continue", e GenerationErrorKind.ProviderUnavailable "模型服务不可用" None true, true
        "error-auth", e GenerationErrorKind.ProviderAuthFailed "密钥无效" (Some "401 Unauthorized") false, false
    ]
