module BrowserImageTests

open System.Reflection
open System.Runtime.InteropServices.JavaScript
open Xunit
open Wanxiang.Interop

/// 与 wwwroot/main.js 的契约：每个公开方法绑定的 JS 函数名都来自旧 BrowserBridge，模块名都是 "wanxiang"。
let private imports (t: System.Type) =
    [
        for m in t.GetMethods(BindingFlags.Public ||| BindingFlags.Static) do
            // 只读元数据：浏览器之外实例化 JSImportAttribute 会抛 PlatformNotSupportedException
            for a in m.GetCustomAttributesData() do
                if a.AttributeType = typeof<JSImportAttribute> then
                    yield m.Name, string a.ConstructorArguments[0].Value, string a.ConstructorArguments[1].Value
    ]
    |> List.sort

[<Fact>]
let ``JS 绑定名与旧 BrowserBridge 一致`` () =
    let expected = [ "Shrink", "imageShrink", "wanxiang" ] |> List.sort
    Assert.Equal<(string * string * string) list>(expected, imports typeof<BrowserImage>)
