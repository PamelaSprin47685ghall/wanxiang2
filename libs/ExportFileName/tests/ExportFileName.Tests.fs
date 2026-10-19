module ExportFileNameTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ExportFileName", "exportfilename.txt"))

/// 金样由旧代码（从 旧 Views/Brand.fs 的 Export.safeFileName 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ExportFileNameRender.render ())

// ---- 旧 ConversationExportTests

[<Fact>]
let ``export filenames stay a bounded filename including long emoji titles`` () =
    Assert.Equal("对话.md", ExportFileName.ofTitle "..")
    let name = ExportFileName.ofTitle "../a\\b\nfile"
    Assert.DoesNotContain("/", name)
    Assert.DoesNotContain("\\", name)
    Assert.DoesNotContain("\n", name)
    let emoji = ExportFileName.ofTitle (String.replicate 100 "👩🏽‍💻")
    Assert.True(Text.Encoding.UTF8.GetByteCount emoji <= 223)
    let baseName = emoji.Substring(0, emoji.Length - 3)
    Assert.Equal(baseName, String.replicate (Globalization.StringInfo.ParseCombiningCharacters(baseName).Length) "👩🏽‍💻")
