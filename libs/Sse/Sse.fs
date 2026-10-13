namespace Wanxiang.Agent

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading
open System.Threading.Channels

/// 一条 SSE 事件。`event:` 缺省时为空串（Gemini 只发 `data:`）。
type SseEvent = { name: string; data: string }

/// Server-Sent Events 分帧（W3C）。只管分帧，不懂任何事件的含义。
module Sse =

    /// 逐帧读取。SSE 以空行分帧，`data:` 可多行需拼接。
    let read (stream: Stream) (ct: CancellationToken) : IAsyncEnumerable<SseEvent> =
        let channel = Channel.CreateUnbounded<SseEvent>()

        let pump =
            task {
                try
                    use reader = new StreamReader(stream, Encoding.UTF8)
                    let data = StringBuilder()
                    let mutable name = ""
                    let mutable finished = false
                    while not finished && not ct.IsCancellationRequested do
                        let! line = reader.ReadLineAsync ct
                        match line with
                        | null ->
                            finished <- true
                            if data.Length > 0 then
                                channel.Writer.TryWrite { name = name; data = data.ToString() } |> ignore
                        | "" ->
                            if data.Length > 0 then
                                channel.Writer.TryWrite { name = name; data = data.ToString() } |> ignore
                                data.Clear() |> ignore
                                name <- ""
                        | text when text.StartsWith(":", StringComparison.Ordinal) -> ()
                        | text when text.StartsWith("event:", StringComparison.Ordinal) ->
                            name <- text.Substring(6).Trim()
                        | text when text.StartsWith("data:", StringComparison.Ordinal) ->
                            if data.Length > 0 then data.Append '\n' |> ignore
                            // W3C SSE 规范：仅剥离冒号后紧跟的一个空格；保留数据正文里的缩进
                            let payload =
                                if text.Length > 5 && text[5] = ' ' then text.Substring 6
                                elif text.Length > 5 then text.Substring 5
                                else ""
                            data.Append payload |> ignore
                        | _ -> ()
                    channel.Writer.Complete()
                with ex -> channel.Writer.Complete ex
            }

        ignore pump
        channel.Reader.ReadAllAsync ct
