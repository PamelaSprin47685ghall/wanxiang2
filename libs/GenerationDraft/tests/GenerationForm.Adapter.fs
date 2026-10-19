module GenerationFormAdapter
open Wanxiang.UI

/// 新库一侧：校验后组载荷，输出形状与旧流程相同（错误表、载荷 JSON 文本）
let run t p m c r (i: string) (a: bool) =
    match GenerationForm.validate t p m c r with
    | Ok v -> [], (GenerationForm.payload v (GenerationForm.instructions i) a).ToJsonString()
    | Error errors -> errors |> List.map (fun (f, msg) -> sprintf "%A" f, msg), ""
