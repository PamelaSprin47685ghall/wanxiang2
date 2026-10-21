namespace Wanxiang.Server

open System
open System.IO
open System.Text

/// 沙箱根目录内的路径解析、文本读取与目录列举。只依赖 BCL；错误以中文文字返回。
module BuiltinToolsCore =

    let maxReadBytes = 1024L * 1024L
    let maxListEntries = 500

    let resolveInRoots (roots: string list) (path: string) : Result<string, string> =
        if List.isEmpty roots then
            Error "文件工具未启用：主机未配置 tools.fileReadRoots 沙箱根目录。"
        elif String.IsNullOrWhiteSpace path then
            Error "路径不能为空。"
        else
            try
                let full = Path.GetFullPath path

                let real =
                    try
                        Path.GetFullPath(
                            File.ResolveLinkTarget(full, true)
                            |> Option.ofObj
                            |> Option.map (fun i -> i.FullName)
                            |> Option.defaultValue full
                        )
                    with _ ->
                        full

                let inside =
                    roots
                    |> List.exists (fun root ->
                        let normalizedRoot =
                            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                            + string Path.DirectorySeparatorChar

                        (real + string Path.DirectorySeparatorChar)
                            .StartsWith(normalizedRoot, StringComparison.Ordinal))

                if inside then
                    Ok real
                else
                    Error(sprintf "路径超出沙箱范围：%s" path)
            with ex ->
                Error(sprintf "路径无效：%s" ex.Message)

    let readFile (roots: string list) (path: string) : string =
        match resolveInRoots roots path with
        | Error e -> sprintf "error: %s" e
        | Ok real ->
            try
                let info = FileInfo real

                if not info.Exists then
                    sprintf "error: 文件不存在：%s" path
                elif info.Length > maxReadBytes then
                    sprintf "error: 文件超过 %d KiB 上限" (maxReadBytes / 1024L)
                else
                    File.ReadAllText(real, Encoding.UTF8)
            with ex ->
                sprintf "error: %s" ex.Message

    let listDirectory (roots: string list) (path: string) : string =
        match resolveInRoots roots path with
        | Error e -> sprintf "error: %s" e
        | Ok real ->
            try
                if not (Directory.Exists real) then
                    sprintf "error: 目录不存在：%s" path
                else
                    let entries =
                        Directory.EnumerateFileSystemEntries real
                        |> Seq.truncate maxListEntries
                        |> Seq.map (fun e ->
                            let name = Path.GetFileName e

                            if Directory.Exists e then
                                name + "/"
                            else
                                try
                                    sprintf "%s (%d B)" name (FileInfo(e).Length)
                                with _ ->
                                    name)
                        |> List.ofSeq

                    if List.isEmpty entries then
                        "（空目录）"
                    else
                        String.Join("\n", entries)
            with ex ->
                sprintf "error: %s" ex.Message
