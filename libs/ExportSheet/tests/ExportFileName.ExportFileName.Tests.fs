module ExportFileNameTests

open System
open System.IO
open Xunit
open Wanxiang.UI


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
