module CardReconcileRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染。
/// 工厂产出「身份#序号」，序号说明是新建还是复用。
let render () =
    let sb = StringBuilder()
    let r = Reconciler<string, string>()
    let mutable made = 0
    for frame in CardReconcileSamples.frames do
        if frame = [ Some "clear" ] then
            r.Clear()
            sb.Append("clear\n") |> ignore
        else
            let desired =
                frame |> List.map (fun k -> k, (fun () -> made <- made + 1; sprintf "%s#%d" (defaultArg k "_") made))
            let plan = r.Reconcile desired
            sb.Append(sprintf "%A -> keep=%d append=%A cached=%d mounted=%A" frame plan.Keep plan.Append r.CachedCount r.MountedKeys).Append('\n') |> ignore
    sb.ToString()
