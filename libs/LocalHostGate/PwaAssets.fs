namespace Wanxiang.App

open System.IO

/// PWA 静态资源目录的查找顺序（从旧 Program.fs 拆出）：
/// 1. 发布包内的 `<程序目录>/pwa`；
/// 2. 开发模式：源码树里 Wanxiang.Pwa 的发布产物 AppBundle（Debug 先于 Release，含 _framework 运行时）；
/// 3. 开发模式：Wanxiang.Pwa/wwwroot 静态壳。
/// 都没有则 None（主机照常启动，只是不托管 PWA）。
module PwaAssets =

    let locate (appBaseDir: string) : string option =
        let candidate = Path.Combine(appBaseDir, "pwa")

        if Directory.Exists candidate then
            Some candidate
        else
            let baseDir = Path.GetFullPath(Path.Combine(appBaseDir, "..", "..", "..", ".."))

            let publishCandidates =
                [
                    Path.Combine(
                        baseDir,
                        "Wanxiang.Pwa",
                        "bin",
                        "Debug",
                        "net10.0-browser",
                        "browser-wasm",
                        "AppBundle"
                    )
                    Path.Combine(
                        baseDir,
                        "Wanxiang.Pwa",
                        "bin",
                        "Release",
                        "net10.0-browser",
                        "browser-wasm",
                        "AppBundle"
                    )
                ]

            match publishCandidates |> List.tryFind Directory.Exists with
            | Some p -> Some(Path.GetFullPath p)
            | None ->
                let dev = Path.Combine(baseDir, "Wanxiang.Pwa", "wwwroot")
                if Directory.Exists dev then Some dev else None
