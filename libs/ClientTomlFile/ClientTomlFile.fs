namespace Wanxiang.App

open System
open System.IO
open System.Text

/// 本机桌面客户端的 client.toml（决策 54：令牌原文只存在客户端这一侧；从旧 Program.fs 拆出）。
/// 文件只有两行有意义：`url = "ws://<listen>/ws"` 与 `token = "<原文>"`。
type ClientTomlFile(path: string) =

    member _.Path = path

    /// 读出令牌原文。文件不存在、读不了、没有 token 行或值为空都是 None。
    /// 与旧实现相同：取第一条以 `token` 开头的行，等号后去空白和引号。
    member _.TryReadToken() : string option =
        if not (File.Exists path) then None
        else
            try
                File.ReadAllLines path
                |> Array.tryPick (fun line ->
                    let t = line.Trim()
                    if t.StartsWith "token" then
                        let v = t.Substring(t.IndexOf '=' + 1).Trim().Trim('"')
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None)
            with _ -> None

    /// 写入 url 与令牌：同目录临时文件（仅本人可读写）+ 改名替换。失败返回错误文字，不抛。
    member _.Write(listen: string, token: string) : Result<unit, string> =
        try
            let text = sprintf "[client]\nurl = \"ws://%s/ws\"\ntoken = \"%s\"\n" listen token
            let dir = Path.GetDirectoryName path
            let tmp = Path.Combine(dir, sprintf ".client.toml.tmp.%d" Environment.ProcessId)
            do
                use fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
                if not (OperatingSystem.IsWindows()) then
                    try File.SetUnixFileMode(tmp, UnixFileMode.UserRead ||| UnixFileMode.UserWrite) with _ -> ()
                let bytes = Encoding.UTF8.GetBytes text
                fs.Write(bytes, 0, bytes.Length)
                fs.Flush()
            File.Move(tmp, path, true)
            Ok()
        with e -> Error e.Message
