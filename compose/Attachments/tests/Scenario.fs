module Scenario

// 金样场景：上传、校验、落位、元数据、下载与错误，加上 stderr 行格式。
// 同一份源码既编进新组合者的测试，也编进旧 AttachmentStore.fs、Stderr.fs 生成金样。

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open Wanxiang.Core
open Wanxiang.Server

let hex (b: byte[]) = Convert.ToHexString(SHA256.HashData b).ToLowerInvariant()
let b64 (b: byte[]) = Convert.ToBase64String b
let gid (n: int) = Guid(sprintf "00000000-0000-0000-0000-%012d" n)

let all () =
    let dir = Path.Combine(Path.GetTempPath(), "wx-att-" + Guid.NewGuid().ToString("N"))
    let out = ResizeArray<string>()
    let show (r: Result<'a, WanxiangError>) =
        match r with
        | Ok v -> out.Add(sprintf "ok %A" v)
        | Error e -> out.Add(sprintf "error %s | %s" (WanxiangError.code e) (WanxiangError.message e))
    let s = AttachmentStore(dir, 1000L, 8)
    let png = Array.append [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |] "rest-of-png"B
    let h = hex png
    show (s.Begin(1, gid 1, int64 png.Length, h.ToUpperInvariant(), "application/octet-stream", " pic\u0001.png "))
    show (s.AppendChunk(gid 1, 0, b64 png[0..7]))
    show (s.AppendChunk(gid 1, 0, b64 png[0..7]))
    show (s.AppendChunk(gid 1, 1, b64 png[8..]))
    show (s.Complete(gid 1, h))
    out.Add(File.ReadAllText(Path.Combine(dir, "attachments", h[0..1], h[2..3], h + ".meta")))
    out.Add(sprintf "%A" (s.Metadata h))
    out.Add(sprintf "exists %b %b" (s.Exists h) (s.Exists "zz"))
    (match s.OpenRead h with Some(st, len) -> out.Add(sprintf "read %d" len); st.Dispose() | None -> out.Add "read none")
    out.Add((s.MakeRef(h, 15L, "image/png", "p.png")).ToJsonString())
    let txt = Text.Encoding.UTF8.GetBytes "hello 你好"
    show (s.Begin(1, gid 2, int64 txt.Length, hex txt, "", "a.txt"))
    show (s.AppendChunk(gid 2, 0, b64 txt[0..7]))
    show (s.AppendChunk(gid 2, 1, b64 txt[8..]))
    show (s.Complete(gid 2, hex txt))
    show (s.Begin(1, gid 3, 3L, hex "abc"B, "text/x", "b"))
    show (s.Complete(gid 3, hex "abc"B))
    show (s.Complete(gid 3, hex "abc"B))
    show (s.Begin(1, gid 4, 3L, hex "abc"B, "", ""))
    show (s.AppendChunk(gid 4, 0, b64 "abd"B))
    show (s.Complete(gid 4, hex "abc"B))
    show (s.Begin(1, gid 5, 3L, hex "abc"B, "image/webp", "c"))
    show (s.AppendChunk(gid 5, 0, b64 "abc"B))
    show (s.Complete(gid 5, hex "abd"B))
    show (s.Begin(1, gid 6, 2000L, hex "abc"B, "", ""))
    show (s.Begin(1, gid 6, -1L, hex "abc"B, "", ""))
    show (s.Begin(1, gid 6, 1L, "nothex", "", ""))
    show (s.AppendChunk(gid 7, 0, "YQ=="))
    show (s.Begin(9, gid 8, 3L, hex "abc"B, "", ""))
    show (s.AppendChunk(gid 8, 0, "!!"))
    s.AbortByConnection 9
    show (s.AppendChunk(gid 8, 0, "YQ=="))
    out.Add(sprintf "tmp %d" (Directory.GetFiles(Path.Combine(dir, "attachments", ".tmp")).Length))
    out.Add(sprintf "%A" (s.Metadata (hex txt)))
    s.Dispose()
    // stderr 行格式：utc 与堆栈随运行变化，统一替换
    Stderr.registerSecrets [ "secret-value-123" ]
    Stderr.write "e" [ "s", box "has secret-value-123"; "i", box 1; "l", box 2L; "f", box 1.5; "x", box (InvalidOperationException "boom secret-value-123") ]
    Stderr.replayTruncated "f.jsonseq" "bad"
    let norm (l: string) =
        let l = Regex.Replace(l, "\"utc\":\"[^\"]*\"", "\"utc\":\"<utc>\"")
        Regex.Replace(l, "\"x.stack\":\"[^\"]*\"", "\"x.stack\":\"<stack>\"")
    for l in Stderr.recentErrorLines () do out.Add(norm l)
    out.Add(Stderr.redact "a secret-value-123 b")
    Stderr.clearSecrets ()
    try Directory.Delete(dir, true) with _ -> ()
    String.concat "\n" out
