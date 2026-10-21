module Tests

open Xunit
open Wanxiang.UI

// 迁自旧 AttachmentVisionTests。
[<Fact>]
let ``validate rejects unsupported image formats and oversize images with guidance`` () =
    match MediaTypes.validate "IMG_0001.HEIC" 1024L with
    | Error m -> Assert.Contains("PNG、JPG、GIF、WebP", m)
    | Ok() -> failwith "heic should be rejected"

    match MediaTypes.validate "big.jpg" (9L * 1024L * 1024L) with
    | Error m ->
        Assert.Contains("8 MiB", m)
        Assert.Contains("压缩", m)
    | Ok() -> failwith "oversize image should be rejected"

    match MediaTypes.validate "empty.txt" 0L with
    | Error m -> Assert.Contains("空", m)
    | Ok() -> failwith "empty should be rejected"

    Assert.Equal(Ok(), MediaTypes.validate "notes.md" 1024L)
    Assert.Equal(Ok(), MediaTypes.validate "photo.jpeg" (7L * 1024L * 1024L))
    Assert.Equal(Ok(), MediaTypes.validate "paper.pdf" (20L * 1024L * 1024L))

[<Fact>]
let ``file name maps to media type, unknown is octet-stream`` () =
    Assert.Equal("image/jpeg", MediaTypes.ofFileName "A.JPG")
    Assert.Equal("application/octet-stream", MediaTypes.ofFileName "x.unknown")
    Assert.Equal("application/octet-stream", MediaTypes.ofFileName "")
    Assert.True(MediaTypes.isImage "image/heic")
    Assert.False(MediaTypes.isModelImage "image/heic")
    Assert.False(MediaTypes.isImage null)

[<Fact>]
let ``picker lists all files first then model images then text`` () =
    let names = MediaTypes.pickerFilters |> List.map (fun (n, _, _) -> n)
    Assert.Equal("所有文件", names.Head)
    let (_, imageExt, _) = MediaTypes.pickerFilters[1]
    Assert.Equal<string list>([ "*.png"; "*.jpg"; "*.jpeg"; "*.gif"; "*.webp" ], imageExt)
    let (_, textExt, _) = MediaTypes.pickerFilters[2]
    Assert.Contains("*.pdf", textExt)
    Assert.DoesNotContain("*.zip", textExt)
