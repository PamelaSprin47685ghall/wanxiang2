/// 金标准渲染：同一份源码分别对旧程序集与新库编译。旧侧在 WANXIANG_STARTUP_TRACE=exit 下运行；
/// 输出行里的毫秒数不确定，只比对去重结果、模式与行格式（新侧的行用固定毫秒、旧侧取实际行后把数字换成 N）。
module Render

open System
open Wanxiang.UI

let private names = [ "firstFrame"; "listReady"; "firstFrame"; "chatReady"; "listReady"; "chatReady" ]

#if INTERACTIVE
let private head () = sprintf "enabled=%b exit=%b" StartupTrace.enabled StartupTrace.exitAfter
let private marks () = names |> List.map StartupTrace.mark
let private shape () =
    let err = Console.Error
    use w = new IO.StringWriter()
    Console.SetError w
    StartupTrace.mark "shape" |> ignore
    Console.SetError err
    Text.RegularExpressions.Regex.Replace(w.ToString().TrimEnd(), "[0-9]+", "N")
#else
let private head () = let e, x = StartupTrace.modeOf "exit" in sprintf "enabled=%b exit=%b" e x
let private marks () = let m = StartupMilestones() in names |> List.map m.Mark
let private shape () = Text.RegularExpressions.Regex.Replace(StartupTrace.line "shape" 1234.6, "[0-9]+", "N")
#endif

let all () =
    String.concat "" [ head () + "\n"; sprintf "%A\n" (marks ()); shape () + "\n" ]
