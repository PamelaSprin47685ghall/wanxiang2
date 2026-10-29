namespace Wanxiang2.MathFigure

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Layout
open Avalonia.VisualTree
open Wanxiang.UI

/// 动态资源绑定辅助：字体与留白走主题资源键（FontFamily.Body / Padding.MathBlock），
/// 主题切换与资源改值由资源系统即时送达，本库不挂活值、不手工订阅。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 公式控件的外观。Ink 是主题管理者的活画笔实例（原地改色不重排），普通记录没有活值字段。
type MathFigureMetrics = { Ink: IBrush }

/// 画一条公式（旧 Rich/MathVisual.fs）。KaTeX HTML 由调用方给（宿主的 RichBackend 产出），本控件只排版与绘制。
/// lineHeight 是所在段落的行高（0 = 未设置）。
type MathFigure(html: string option, displayMode: bool, fontSize: float, look: MathFigureMetrics) as this =
    inherit Control()

    let mutable body: FontFamily = FontFamily.Default
    let mutable picture: MathPicture option = None

    let relayout () =
        picture <-
            match html, body with
            | Some h, f when not (isNull f) ->
                match MathDraw.tryLayout f look.Ink fontSize h with
                | Ok p -> Some p
                | Error _ -> None
            | _ -> None

    do
        // 正文字体：把主题资源键接到可继承的 TextElement.FontFamily attached 属性上。
        let applyFont (f: FontFamily) =
            if not (obj.ReferenceEquals(f, body)) && not (isNull f) then
                body <- f
                relayout ()
                this.InvalidateMeasure()
                this.InvalidateVisual()
        // 构造时先从应用级资源取初值（未上屏也要能量版，调用方凭 HasLayout 决定退化）；
        // 之后资源改值 → 属性变化 → PropertyChanged 回调重排（声明式，无活值订阅）。
        body <-
            match Application.Current with
            | null -> FontFamily.Default
            | app ->
                match app.TryFindResource(box "FontFamily.Body") with
                | true, (:? FontFamily as f) -> f
                | _ -> FontFamily.Default

        relayout ()
        this.Bind(TextElement.FontFamilyProperty, Dyn.res "FontFamily.Body") |> ignore

        this.PropertyChanged.Add(fun e ->
            if e.Property = TextElement.FontFamilyProperty then
                applyFont (e.NewValue :?> FontFamily))

    member _.HasLayout = picture.IsSome

    member _.Picture = picture

    /// 盒宽、盒高、盒内基线位置：只按公式自身的墨迹范围算。
    member private _.Metrics =
        MathBaseline.metrics (picture |> Option.map (fun p -> p.Width, p.Above, p.Below))

    /// 所在段落文本块的真实首行基线。必须问父级：控件高度会反过来撑大行盒，任何预估都是循环。
    member private this.HostBaseline: float option =
        let rec climb (visual: Visual) =
            match visual with
            | null -> None
            | :? TextBlock as host ->
                match host.TextLayout with
                | null -> None
                | layout -> layout.TextLines |> Seq.tryHead |> Option.map (fun line -> line.Baseline)
            | other -> climb (other.GetVisualParent())

        climb (this.GetVisualParent())

    /// 公式基线到控件顶缘的距离。行内时以父级文本块的真实基线为准。
    member this.Baseline =
        let _, _, own = this.Metrics
        MathBaseline.drawBaseline displayMode own this.HostBaseline this.Bounds.Y

    override this.MeasureOverride(_) =
        let width, height, _ = this.Metrics
        Size(width, height)

    override this.Render(context) =
        match picture with
        | Some laid ->
            // 声明的尺寸就是这块公式的全部可见范围：字形抗锯齿可能比排版盒多出半个像素，
            // 画到盒外会侵入相邻行或别的公式。裁回自己的盒，布局与基线不受影响。
            use _ = context.PushClip(Rect(0.0, 0.0, this.Bounds.Width, this.Bounds.Height))
            use _ = context.PushTransform(Matrix.CreateTranslation(0.0, this.Baseline))

            for mark in laid.Marks do
                match mark with
                | MathMark.Glyph(_, shaped, _, topLeft) -> context.DrawText(shaped, topLeft)
                | MathMark.Rule rect -> context.FillRectangle(look.Ink, rect)
                | MathMark.Path(geometry, transform, clip) ->
                    use _ = context.PushClip clip
                    use _ = context.PushTransform transform
                    context.DrawGeometry(look.Ink, null, geometry)
        | None -> ()

/// 公式的对外入口（旧 MathRender）：能排就给控件，排不出来返回 None，由调用方退化成原文。
module MathFigures =

    let private describe (figure: MathFigure) (tex: string) =
        // 自绘公式对读屏只是一块空白：报出原式并给悬停提示
        Avalonia.Automation.AutomationProperties.SetName(figure, sprintf "公式：%s" tex)
        ToolTip.SetTip(figure, tex)

    /// 行内公式。字号跟随所在段落。
    let tryInline (look: MathFigureMetrics) (html: string option) (fontSize: float) (tex: string) : Control option =
        let figure = new MathFigure(html, false, fontSize, look)

        if figure.HasLayout then
            describe figure tex
            Some(figure :> Control)
        else
            None

    /// 行内公式的内联容器。对齐必须显式指定：默认值随主题模板变化，基线对齐是可见的正确性问题。
    let inlineContainer (control: Control) =
        let container = Documents.InlineUIContainer control
        container.BaselineAlignment <- BaselineAlignment.Bottom
        container

    /// 展示式公式：独占一行、水平居中，上下留白取自 Look。
    let tryBlock (look: MathFigureMetrics) (html: string option) (fontSize: float) (tex: string) : Control option =
        let figure = new MathFigure(html, true, fontSize, look)

        if not figure.HasLayout then
            None
        else
            describe figure tex
            figure.HorizontalAlignment <- Layout.HorizontalAlignment.Center

            let host =
                Border(Child = figure, HorizontalAlignment = Layout.HorizontalAlignment.Stretch)
            // 展示式上下留白：主题资源键直达 Padding，随主题即时生效
            host.Bind(Border.PaddingProperty, Dyn.res "Padding.MathBlock") |> ignore
            Some(host :> Control)
