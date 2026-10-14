namespace Wanxiang2.MarkdownView

open System
open Avalonia.Controls

/// 活值只在控件挂进可视树期间订阅，摘下即退订：流式渲染不断丢弃控件，长寿的主题实例不能攥着它们。
module internal Follow =

    let private current (source: IObservable<'T>) =
        let mutable v = Unchecked.defaultof<'T>
        (source.Subscribe(fun x -> v <- x)).Dispose()
        v

    /// 活值的当前值
    let now (source: IObservable<'T>) = current source

    /// 立即按当前值应用一次，之后随挂载订阅。
    let live (control: Control) (source: IObservable<'T>) (apply: 'T -> unit) =
        apply (current source)
        let mutable sub: IDisposable = null
        control.AttachedToVisualTree.Add(fun _ -> if isNull sub then sub <- source.Subscribe apply)
        control.DetachedFromVisualTree.Add(fun _ ->
            if not (isNull sub) then
                sub.Dispose()
                sub <- null)

    /// 两个活值合成一次应用
    let live2 (control: Control) (a: IObservable<'A>) (b: IObservable<'B>) (apply: 'A -> 'B -> unit) =
        let mutable va = current a
        let mutable vb = current b
        live control a (fun x -> va <- x; apply va vb)
        live control b (fun y -> vb <- y; apply va vb)

    /// 三个活值合成一次应用
    let live3 control (a: IObservable<'A>) (b: IObservable<'B>) (c: IObservable<'C>) (apply: 'A -> 'B -> 'C -> unit) =
        let mutable va = current a
        let mutable vb = current b
        let mutable vc = current c
        live control a (fun x -> va <- x; apply va vb vc)
        live control b (fun y -> vb <- y; apply va vb vc)
        live control c (fun z -> vc <- z; apply va vb vc)
