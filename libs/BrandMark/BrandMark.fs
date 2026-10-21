namespace Wanxiang2.BrandMark

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform

/// 品牌标渲染（迁自旧 Views/Brand.fs 的 Brand 模块；Export 部分早已在 TranscriptMarkdown / ExportFileName）。
module BrandMark =

    /// 找图：先内嵌资源（browser-wasm 没有宿主文件系统），再按顺序找本地文件；都没有就 None。
    /// 资源地址与候选路径由组合者给（旧代码写死 avares://Wanxiang.UI/Assets/logo.png 与 WANXIANG_HOME 等）。
    let tryLoad (asset: Uri option) (files: string list) : Bitmap option =
        let fromAsset () =
            asset
            |> Option.bind (fun uri ->
                try
                    use stream = AssetLoader.Open uri
                    Some(new Bitmap(stream))
                with _ ->
                    None)

        match fromAsset () with
        | Some b -> Some b
        | None ->
            try
                files
                |> List.map IO.Path.GetFullPath
                |> List.tryFind IO.File.Exists
                |> Option.map (fun path -> new Bitmap(path))
            with _ ->
                None

    /// 圆角方形品牌标；没有图时退化成「万」字。
    /// 有图时不垫底色（资产自带透明底）；Stretch 用 Uniform，非方形图只露底不裁剪。
    let tile (image: IImage option) (size: float) : Control =
        let tile =
            Border(Width = size, Height = size, ClipToBounds = true, CornerRadius = CornerRadius(size * 0.22))

        match image with
        | Some img ->
            tile.Background <- Brushes.Transparent
            tile.Child <- Image(Source = img, Stretch = Stretch.Uniform)
        | None ->
            tile.Classes.Add "surface"

            let glyph =
                TextBlock(
                    Text = "万",
                    FontWeight = FontWeight.Medium,
                    FontSize = size * 0.42,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                )

            glyph.Classes.Add "brand-glyph"
            tile.Child <- glyph

        tile :> Control
