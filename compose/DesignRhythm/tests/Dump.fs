module Dump
open System
open System.Reflection
open System.Text

// 用反射列出模块（含嵌套模块）里所有公开值；函数在一组输入上求值
let f (v: obj) =
    match v with
    | :? float as x -> x.ToString("R", Globalization.CultureInfo.InvariantCulture)
    | :? (float list) as l -> l |> List.map (fun x -> x.ToString("R", Globalization.CultureInfo.InvariantCulture)) |> String.concat ";"
    | null -> "null"
    | o -> string o

let rec dumpType (sb: StringBuilder) (prefix: string) (t: Type) =
    for p in t.GetProperties(BindingFlags.Public ||| BindingFlags.Static) |> Array.sortBy (fun p -> p.Name) do
        sb.Append(sprintf "%s.%s = %s\n" prefix p.Name (f (p.GetValue null))) |> ignore
    for n in t.GetNestedTypes(BindingFlags.Public) |> Array.sortBy (fun n -> n.Name) do
        dumpType sb (prefix + "." + n.Name.Replace("Module", "")) n

let render (types: Type list) (probes: (string * obj) list) =
    let sb = StringBuilder()
    for t in types do dumpType sb t.Name t
    for (k, v) in probes do sb.Append(sprintf "%s = %s\n" k (f v)) |> ignore
    sb.ToString()
