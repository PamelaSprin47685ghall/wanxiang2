module Scenario

open System
open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.AI
open Wanxiang.Config
open Wanxiang.Agent

type Recorder(sse: string) =
    inherit HttpMessageHandler()
    member val Seen = ResizeArray<string>()
    override this.SendAsync(request, _ct) =
        task {
            let! body = request.Content.ReadAsStringAsync()
            let headers = request.Headers |> Seq.map (fun h -> h.Key + "=" + String.Join(",", h.Value)) |> Seq.sort |> String.concat ";"
            this.Seen.Add(sprintf "%s %s\n%s\n%s" (string request.Method) (string request.RequestUri) headers body)
            let r = new HttpResponseMessage(HttpStatusCode.OK)
            r.Content <- new StringContent(sse, Encoding.UTF8, "text/event-stream")
            return r
        }

let provider kind url =
    { id = "p"; kind = kind; label = ""; baseUrl = url; apiKey = Some "KEY"; models = [ "m" ]; defaultModel = "m"
      timeoutSeconds = 120; maxRetries = 0; enabled = true; promptCaching = true; headers = Map.empty; extraJson = None; visionOverrides = Map.empty }

let messages () : ChatMessage list =
    let args = Dictionary<string, obj>()
    args["q"] <- box (JsonValue.Create "天气")
    [ ChatMessage(ChatRole.System, "你是助手")
      ChatMessage(ChatRole.User, [| TextContent "看图" :> AIContent; DataContent(ReadOnlyMemory(Array.init 4 byte), "image/png") :> AIContent |])
      ChatMessage(ChatRole.Assistant, [| TextContent "查一下" :> AIContent; FunctionCallContent("call1", "search", args) :> AIContent |])
      ChatMessage(ChatRole.Tool, [| FunctionResultContent("call1", box "{\"ok\":true}") :> AIContent |])
      ChatMessage(ChatRole.User, "继续") ]

let options () =
    let o = ChatOptions(Temperature = Nullable 0.5f, MaxOutputTokens = Nullable 300)
    o.AdditionalProperties <- AdditionalPropertiesDictionary()
    o.AdditionalProperties["service_tier"] <- box (JsonValue.Create "auto")
    o

let describe (u: ChatResponseUpdate) =
    let parts =
        [ for c in u.Contents ->
            match c with
            | :? TextReasoningContent as r -> "reasoning:" + r.Text
            | :? TextContent as t -> (if isNull t.RawRepresentation then "text:" else "text(" + string t.RawRepresentation + "):") + t.Text
            | :? FunctionCallContent as f ->
                let a = f.Arguments |> Seq.map (fun kv -> kv.Key + "=" + string kv.Value) |> String.concat ","
                sprintf "call:%s:%s(%s)" f.CallId f.Name a
            | :? UsageContent as uc -> sprintf "usage:%A/%A/%A" uc.Details.InputTokenCount uc.Details.OutputTokenCount uc.Details.TotalTokenCount
            | other -> other.GetType().Name ]
    sprintf "[%s] id=%s finish=%s" (String.concat " | " parts) (if isNull u.ResponseId then "" else u.ResponseId) (if u.FinishReason.HasValue then string u.FinishReason.Value else "")

let anthropicSse =
    String.concat "\n"
        [ "event: message_start"; """data: {"type":"message_start","message":{"id":"msg_1","usage":{"input_tokens":11,"output_tokens":1}}}"""; ""
          "event: content_block_delta"; """data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"想"}}"""; ""
          "event: content_block_delta"; """data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"你好"}}"""; ""
          "event: content_block_start"; """data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"tu_1","name":"search"}}"""; ""
          "event: content_block_delta"; """data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"q\":"}}"""; ""
          "event: content_block_delta"; """data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"\"雨\"}"}}"""; ""
          "event: content_block_stop"; """data: {"type":"content_block_stop","index":1}"""; ""
          "event: message_delta"; """data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"output_tokens":42}}"""; ""
          "event: message_stop"; """data: {"type":"message_stop"}"""; ""; "" ]

let geminiSse =
    String.concat "\n"
        [ """data: {"candidates":[{"content":{"parts":[{"text":"想","thought":true},{"text":"你好"}]}}]}"""; ""
          """data: {"candidates":[{"content":{"parts":[{"functionCall":{"name":"search","args":{"q":"雨"}}}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":7,"candidatesTokenCount":3,"totalTokenCount":12}}"""; ""; "" ]

let run (client: IChatClient) =
    let updates = ResizeArray<string>()
    let e = client.GetStreamingResponseAsync(messages (), options (), CancellationToken.None).GetAsyncEnumerator()
    while e.MoveNextAsync().AsTask().Result do updates.Add(describe e.Current)
    updates

let all () =
    let out = ResizeArray<string>()
    let ha = new Recorder(anthropicSse)
    let a = new AnthropicChatClient(provider "anthropic" "https://api.anthropic.com", "claude-x", new HttpClient(ha), 1024) :> IChatClient
    let ua = run a
    out.Add("== anthropic request"); out.AddRange ha.Seen; out.Add "== anthropic updates"; out.AddRange ua
    let hg = new Recorder(geminiSse)
    let g = new GeminiChatClient(provider "gemini" "https://generativelanguage.googleapis.com", "gemini-x", new HttpClient(hg), 512) :> IChatClient
    let ug = run g
    out.Add("== gemini request"); out.AddRange hg.Seen; out.Add "== gemini updates"; out.AddRange ug
    String.concat "\n" out
