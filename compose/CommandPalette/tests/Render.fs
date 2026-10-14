/// 金标准渲染：同一份源码分别对旧代码摘录（tools/OldShellPalette.fs）与本组合者编译。
/// 对话条目的时间按本地时区写，生成与比对都在 TZ=UTC 下。
module Render

open System
open Wanxiang.UI

#if INTERACTIVE
let private run (input: ShellPaletteInput) (id: string) = ShellPalette.run input id
#else
let private run (_: ShellPaletteInput) (id: string) =
    let c = ShellPalette.commandOf id
    c, ShellPalette.closesDrawer c
#endif

let private g (n: int) = Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)
let private at = DateTimeOffset(2026, 10, 14, 9, 5, 0, TimeSpan.Zero)

let private summary n title pinned archived isFork : ConversationSummary =
    { id = g n; title = title; preview = ""; running = false; pinned = pinned; archived = archived
      createdAt = at; updatedAt = at.AddDays(float n); messageCount = 1; isFork = isFork
      providerId = "p"; model = "m"; lastCommitId = 1UL }

let private sections =
    [ "Providers", "模型服务", "主机"; "Tools", "工具", "主机"; "Generation", "对话默认值", "主机"; "Devices", "设备", "主机"
      "Appearance", "外观与操作", "这台客户端"; "About", "关于", "这台客户端" ]

let inputs : ShellPaletteInput list =
    let full =
        { hasConversation = true
          presets = [ "p1", "写作", "gpt · 温度 0.7"; "p2", "代码", "本机模型" ]
          supportsPresets = true
          supportsImport = true
          themeLabel = "跟随系统"
          sections = sections
          conversations = [ summary 1 "旅行" true false false; summary 2 "" false true true; summary 3 "归档分叉" true true true ]
          showArchived = false }
    [ full
      { full with hasConversation = false; supportsPresets = false; supportsImport = false; presets = []; themeLabel = "深色" }
      { full with supportsPresets = false; showArchived = true } ]

let ids =
    [ "act:search-messages"; "conv:" + string (g 1); "conv:not-a-guid"; "set:providers"; "set:appearance:主题"; "set:devices:配对新设备"
      "act:new"; "preset:p1"; "act:save-preset"; "act:model"; "act:export"; "act:fork"; "act:copy-reply"; "act:regenerate"
      "act:import"; "act:theme"; "act:sidebar"; "act:shortcuts"; "act:settings"; "act:add-provider"; "act:unknown"; "zzz" ]

let all () =
    let lines = ResizeArray<string>()
    for i, input in List.indexed inputs do
        for item in ShellPalette.items input do
            lines.Add(sprintf "%d %s|%A|%s|%s|%A|%A" i item.id item.kind item.title item.subtitle item.keywords item.shortcut)
    for id in ids do
        lines.Add(sprintf "run %s => %A" id (run inputs.Head id))
    lines |> Seq.map (fun l -> l + "\n") |> String.concat ""
