module Scenario

open System
open System.Text.Json.Nodes
open Wanxiang.Config
open Wanxiang.Server

/// 一串配置变更：每步打印结果（成功给序列化后的 TOML，失败给错误列表），失败不改变当前配置。
let all () : string =
    let sb = Text.StringBuilder()
    let mutable cfg = AppConfig.defaults (Guid "11111111-2222-3333-4444-555555555555")

    let step (name: string) (f: AppConfig -> Result<AppConfig, string list>) =
        sb.AppendLine("## " + name) |> ignore

        match f cfg with
        | Ok c ->
            cfg <- c
            sb.AppendLine(TomlCodec.serialize c) |> ignore
        | Error es -> sb.AppendLine("ERROR " + String.Join(" | ", es)) |> ignore

    let j (s: string) = JsonNode.Parse(s).AsObject()

    step "provider ok" (fun c ->
        ConfigMutation.upsertProvider
            c
            (j
                """{"id":"oa","kind":"openai","baseUrl":"https://x/v1","apiKey":" k ","models":["m1","m1"," ","m2"],"enabled":true,"headers":{"X-A":"1","n":2},"timeoutSeconds":30}"""))

    step "provider bad id" (fun c -> ConfigMutation.upsertProvider c (j """{"id":"bad id!","kind":"openai"}"""))

    step "provider bad kind" (fun c ->
        ConfigMutation.upsertProvider c (j """{"id":"p2","kind":"nope","baseUrl":"x"}"""))

    step "provider keep key" (fun c ->
        ConfigMutation.upsertProvider c (j """{"id":"oa","kind":"openai","baseUrl":"https://y/v1","models":["m3"]}"""))

    step "provider gemini" (fun c ->
        ConfigMutation.upsertProvider
            c
            (j
                """{"id":"gm","kind":"gemini","baseUrl":"https://g","apiKey":"g","models":["gemini-2"],"temperature":0.3}"""))

    step "mcp stdio" (fun c ->
        ConfigMutation.upsertMcp
            c
            (j
                """{"id":"fs","transport":"stdio","command":"/bin/sh","args":["-c","x"],"env":{"A":"b"},"enabled":true}"""))

    step "mcp http" (fun c ->
        ConfigMutation.upsertMcp c (j """{"id":"web","transport":"http","url":"http://h/mcp","headers":{"T":"v"}}"""))

    step "mcp bad" (fun c -> ConfigMutation.upsertMcp c (j """{"id":"z","transport":"carrier-pigeon"}"""))
    step "mcp no command" (fun c -> ConfigMutation.upsertMcp c (j """{"id":"z","transport":"stdio"}"""))

    step "tools" (fun c ->
        ConfigMutation.updateTools
            c
            (j """{"defaultPolicy":"ask","policies":{"fs.read":"allow","x":"deny","y":"weird"},"timeoutSeconds":5}"""))

    step "tools empty" (fun c -> ConfigMutation.updateTools c (j """{}"""))

    step "generation" (fun c ->
        ConfigMutation.updateGeneration
            c
            (j
                """{"defaultProvider":"oa","defaultModel":"m3","maxContextMessages":40,"temperature":0.7,"systemPrompt":"hi"}"""))

    step "generation bad provider" (fun c ->
        ConfigMutation.updateGeneration c (j """{"defaultProvider":"ghost","defaultModel":"m"}"""))

    step "preset" (fun c ->
        ConfigMutation.upsertPreset
            c
            (j
                """{"id":"p1","name":"Coder","providerId":"oa","model":"m3","systemPrompt":"code","tools":["fs.read"]}"""))

    step "preset bad" (fun c -> ConfigMutation.upsertPreset c (j """{"id":"p2","name":" "}"""))
    step "delete provider in use" (fun c -> ConfigMutation.deleteProvider c "oa")
    step "delete preset" (fun c -> ConfigMutation.deletePreset c "p1")
    step "delete missing preset" (fun c -> ConfigMutation.deletePreset c "nope")
    step "delete mcp" (fun c -> ConfigMutation.deleteMcp c "web")
    step "delete missing mcp" (fun c -> ConfigMutation.deleteMcp c "nope")
    step "delete provider gm" (fun c -> ConfigMutation.deleteProvider c "gm")
    step "delete missing provider" (fun c -> ConfigMutation.deleteProvider c "nope")
    sb.ToString()
