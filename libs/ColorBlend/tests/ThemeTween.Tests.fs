module ThemeTweenTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ThemeTween", "themetween.txt"))
let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
let gray v = { A = 255uy; R = v; G = v; B = v }

/// 金样由旧代码（旧 Design/Tokens.fs apply / advanceFrame，照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ThemeTweenRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``brushes tween to the target and land exactly at 200 ms`` () =
    let tw = ThemeTween<int>()
    Assert.Equal(ThemeStart.Tween, tw.Apply(t0, false, 200.0, [ 1, gray 0uy, gray 200uy ]))
    let mid = tw.Frame(t0.AddMilliseconds 100.0, 200.0)
    Assert.Equal(gray 100uy, snd mid.Colors.Head)
    let last = tw.Frame(t0.AddMilliseconds 200.0, 200.0)
    Assert.True last.Finished
    Assert.Equal(gray 200uy, snd last.Colors.Head)

[<Fact>]
let ``the latest switch takes over from the logical current color`` () =
    let tw = ThemeTween<int>()
    tw.Apply(t0, false, 200.0, [ 1, gray 0uy, gray 200uy ]) |> ignore
    // 中途没有帧也照样从逻辑当前色（100）起步
    tw.Apply(t0.AddMilliseconds 100.0, false, 200.0, [ 1, gray 0uy, gray 0uy ]) |> ignore
    let f = tw.Frame(t0.AddMilliseconds 100.0, 200.0)
    Assert.Equal(gray 100uy, snd f.Colors.Head)

[<Fact>]
let ``reduced motion and replaying the same theme snap`` () =
    let tw = ThemeTween<int>()
    Assert.Equal(ThemeStart.Snap, tw.Apply(t0, false, 0.0, [ 1, gray 0uy, gray 9uy ]))
    Assert.Equal(ThemeStart.Snap, tw.Apply(t0, true, 200.0, [ 1, gray 0uy, gray 9uy ]))
    Assert.False tw.IsRunning
