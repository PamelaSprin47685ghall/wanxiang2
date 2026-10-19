module PwaAssetsTests
open System
open System.IO
open Xunit
open Wanxiang.App

let private tree f =
    let root = Path.Combine(Path.GetTempPath(), "wx-pwa-" + Guid.NewGuid().ToString("N"))
    // 模拟 src/Wanxiang.App/bin/Debug/net10.0/
    let app = Path.Combine(root, "Wanxiang.App", "bin", "Debug", "net10.0")
    Directory.CreateDirectory app |> ignore
    try f root app finally Directory.Delete(root, true)

let private mk (p: string) = Directory.CreateDirectory p |> ignore

[<Fact>]
let ``什么都没有时为 None`` () = tree (fun _ app -> Assert.Equal(None, PwaAssets.locate app))

[<Fact>]
let ``发布包内的 pwa 目录最优先`` () =
    tree (fun root app ->
        mk (Path.Combine(app, "pwa"))
        mk (Path.Combine(root, "Wanxiang.Pwa", "wwwroot"))
        Assert.Equal(Some(Path.Combine(app, "pwa")), PwaAssets.locate app))

[<Fact>]
let ``开发模式：Debug 产物先于 Release，先于 wwwroot`` () =
    tree (fun root app ->
        let bundle cfg = Path.Combine(root, "Wanxiang.Pwa", "bin", cfg, "net10.0-browser", "browser-wasm", "AppBundle")
        mk (Path.Combine(root, "Wanxiang.Pwa", "wwwroot"))
        Assert.Equal(Some(Path.Combine(root, "Wanxiang.Pwa", "wwwroot")), PwaAssets.locate app)
        mk (bundle "Release")
        Assert.Equal(Some(bundle "Release"), PwaAssets.locate app)
        mk (bundle "Debug")
        Assert.Equal(Some(bundle "Debug"), PwaAssets.locate app))
