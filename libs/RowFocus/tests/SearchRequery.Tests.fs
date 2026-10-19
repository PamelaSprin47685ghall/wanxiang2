module SearchRequeryTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "SearchRequery", "searchrequery.txt"))

/// 金样由旧代码（旧 Views/Sidebar.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), SearchRequeryRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``a late tick for the same query does not requery`` () =
    Assert.False(SearchRequery.shouldQuery " abc " "abc")
    Assert.True(SearchRequery.shouldQuery "abd" "abc")
    // 空词总是走一遍（清空命中）
    Assert.True(SearchRequery.shouldQuery "" "")
