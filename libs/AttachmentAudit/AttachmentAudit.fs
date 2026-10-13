namespace Wanxiang.Server

/// 诊断检查「missing-attachments」「orphan-attachments」（从旧 Doctor 拆出）：
/// missing = 被消息引用但 blob 不存在；orphan = blob 存在但没有任何引用（`.meta` 文件不算 blob）。
module AttachmentAudit =

    /// `refs`：按消息顺序的 (sha256, blob 是否存在)；`blobNames`：附件目录里的文件名（按目录遍历顺序）。
    /// 返回 (missing, orphan) 两项检查各自的 (ok, detail)。
    let run (refs: (string * bool) seq) (blobNames: string seq) : (bool * string) * (bool * string) =
        let referenced = System.Collections.Generic.HashSet<string>()
        let missing = System.Collections.Generic.HashSet<string>()
        for (sha, exists) in refs do
            referenced.Add sha |> ignore
            if not exists then missing.Add sha |> ignore
        let orphan = blobNames |> Seq.filter (fun n -> not (n.EndsWith ".meta") && not (referenced.Contains n)) |> List.ofSeq
        let m = missing.Count
        (m = 0, (if m = 0 then "ok" else sprintf "%d referenced blob(s) missing" m)),
        (List.isEmpty orphan, (if List.isEmpty orphan then "ok" else sprintf "%d unreferenced blob(s)" orphan.Length))
