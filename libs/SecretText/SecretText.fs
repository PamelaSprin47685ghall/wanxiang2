namespace Wanxiang.Server

open System

/// 文本脱敏规则：已知凭据值精确替换为 `***`；按「键 = 值」形态兜底打码。
/// 支持包的配置脱敏用它（从旧 SupportBundle 拆出）；stderr 的精确替换（JsonLinesLog.Redact）是同一条规则，
/// 但那边按长度降序替换（库不引用兄弟库，各持一份；差别只在一个密钥是另一个子串时）。
module SecretText =

    /// 按给定顺序把每个出现的凭据值替换为 `***`；空白值跳过。
    let replaceKnown (secrets: string seq) (text: string) : string =
        let mutable s = text
        for secret in secrets do
            if not (String.IsNullOrWhiteSpace secret) && s.Contains secret then s <- s.Replace(secret, "***")
        s

    /// 行首（去缩进后）以任一前缀开头且含 `=` 的行，`=` 之后整体换成 ` "***"`。
    let maskAssignments (prefixes: string list) (text: string) : string =
        text.Split('\n')
        |> Array.map (fun line ->
            let t = line.TrimStart()
            if prefixes |> List.exists (fun p -> t.StartsWith p) then
                let idx = line.IndexOf '='
                if idx >= 0 then line.Substring(0, idx + 1) + " \"***\""
                else line
            else line)
        |> String.concat "\n"
