namespace Wanxiang.App

open System.Collections.Generic
open Jint

/// 进程内的 JS 宿主（Jint：纯托管解释器，不引入原生依赖；从旧 RichJint.fs 拆出引擎部分）。
/// 对象自己拥有引擎与「哪些脚本已加载」：同一个键只执行一次。Jint 的 Engine 不是线程安全的，
/// 所有调用在对象内的锁里串行。它不知道 hljs 或 KaTeX。
type JintScriptHost() =
    let gate = obj ()
    let mutable engine: Engine option = None
    let loaded = HashSet<string>()

    let ensureEngine () =
        match engine with
        | Some existing -> existing
        | None ->
            let created =
                new Engine(fun options -> options.Strict(false).LimitRecursion 10000 |> ignore)
            // 垫一个什么都不做的 console：脚本里的 warn 在裸引擎里会抛穿
            created.Execute(
                "var console={log:function(){},warn:function(){},error:function(){},"
                + "debug:function(){},info:function(){},trace:function(){}};"
            )
            |> ignore

            engine <- Some created
            created

    /// 以 `key` 为名在全局作用域执行一次脚本；已执行过就跳过。`source` 只在需要时才取。
    member _.LoadOnce(key: string, source: unit -> string) : unit =
        lock gate (fun () ->
            let host = ensureEngine ()

            if not (loaded.Contains key) then
                host.Execute(source ()) |> ignore
                loaded.Add key |> ignore)

    member _.IsLoaded(key: string) =
        lock gate (fun () -> loaded.Contains key)

    /// 绑定全局变量后求值一个返回字符串的表达式。变量走 SetValue 而不是拼进表达式：值里什么引号都可能有。
    /// 脚本抛错时异常原样抛出，由调用方决定退化方式。
    member _.EvaluateString(expression: string, bindings: (string * obj) list) : string =
        lock gate (fun () ->
            let host = ensureEngine ()

            for (name, value) in bindings do
                host.SetValue(name, value) |> ignore

            host.Evaluate(expression).AsString())
