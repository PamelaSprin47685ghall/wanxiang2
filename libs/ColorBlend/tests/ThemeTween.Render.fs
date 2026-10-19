module ThemeTweenRender
open System
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染。宿主替身记住每支画笔的当前色。
let render () =
    let sb = StringBuilder()
    let tw = ThemeTween<string>()
    let brushes = Collections.Generic.Dictionary<string, Argb>()
    for k, v in ThemeTweenSamples.light do brushes[k] <- v
    let mutable mode = ThemeTweenSamples.light
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let show (cs: (string * Argb) list) =
        cs |> List.map (fun (k, a) -> sprintf "%s=%d,%d,%d,%d" k a.A a.R a.G a.B) |> String.concat " "
    for act, ms in ThemeTweenSamples.script do
        let now = t0.AddMilliseconds ms
        let write cs = for k, v in cs do brushes[k] <- v
        let apply next sameMode duration =
            let triples = next |> List.map (fun (k, t) -> k, brushes[k], t)
            mode <- next
            match tw.Apply(now, sameMode, duration, triples) with
            | ThemeStart.Snap -> write next; "snap"
            | ThemeStart.Tween -> "tween"
        let r =
            match act with
            | "dark" -> apply ThemeTweenSamples.dark (mode = ThemeTweenSamples.dark) ThemeTween<string>.DurationMs
            | "light" -> apply ThemeTweenSamples.light (mode = ThemeTweenSamples.light) ThemeTween<string>.DurationMs
            | "same" -> apply mode true ThemeTween<string>.DurationMs
            | "reduced" -> apply ThemeTweenSamples.light (mode = ThemeTweenSamples.light) 0.0
            | "frame" | "frameReduced" ->
                let f = tw.Frame(now, (if act = "frame" then ThemeTween<string>.DurationMs else 0.0))
                write f.Colors
                sprintf "finished=%b n=%d" f.Finished f.Colors.Length
            | _ -> let cs = tw.Complete() in write cs; "complete"
        let state = [ for KeyValue(k, v) in brushes -> k, v ] |> List.sortBy fst
        sb.Append(sprintf "%s %g -> %s running=%b | %s" act ms r tw.IsRunning (show state)).Append('\n') |> ignore
    sb.Append(sprintf "%g %d" ThemeTween<string>.DurationMs ThemeTween<string>.FrameTicks).Append('\n') |> ignore
    sb.ToString()
