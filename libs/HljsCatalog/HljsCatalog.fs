namespace Wanxiang.UI

open System
open System.Text.Json

/// hljs 语言目录：哪些语言在基座里、哪些要按需加载、```ts / ```F# 这类写法对应的规范名
/// （从旧 RichAssets 拆出；目录文本来自 `hljs.aliases.json`，由调用方给出）。
type HljsCatalog =
    { /// 基座里已注册的语言，直接可用
      builtin: Set<string>
      /// 需要额外加载一个脚本才能注册的语言
      onDemand: Set<string>
      /// 语言标注（小写）→ hljs 规范名
      aliases: Map<string, string> }

module HljsCatalog =

    let empty = { builtin = Set.empty; onDemand = Set.empty; aliases = Map.empty }

    /// 解析目录 JSON；格式不对时退化为空目录（全部不着色），绝不抛。
    let parse (json: string) : HljsCatalog =
        try
            use doc = JsonDocument.Parse json
            let strings (name: string) =
                doc.RootElement.GetProperty(name).EnumerateArray()
                |> Seq.map (fun e -> e.GetString())
                |> Set.ofSeq
            let aliases =
                doc.RootElement.GetProperty("aliases").EnumerateObject()
                |> Seq.map (fun p -> p.Name, p.Value.GetString())
                |> Map.ofSeq
            { builtin = strings "builtin"; onDemand = strings "onDemand"; aliases = aliases }
        with _ -> empty

    /// 语言标注 → hljs 规范名：先查别名，再认基座语言；认不出为 None（调用方退化为不着色）。
    /// 注意与旧实现相同：只在 builtin 里找规范名本身，按需语言必须经别名表到达。
    let resolve (catalog: HljsCatalog) (language: string) : string option =
        if String.IsNullOrWhiteSpace language then None
        else
            let key = language.Trim().ToLowerInvariant()
            match catalog.aliases.TryFind key with
            | Some canonical -> Some canonical
            | None -> if catalog.builtin.Contains key then Some key else None

    let requiresLoad (catalog: HljsCatalog) (canonical: string) = catalog.onDemand.Contains canonical
