namespace Wanxiang2.IconSet

open System
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open IconPacks.Avalonia.Lucide

/// 万象用到的图标，按用途命名（旧 Design/Icons.fs 的语义映射）。
[<RequireQualifiedAccess>]
type Glyph =
    | Search
    | Plus
    | Minus
    | Close
    | Check
    | CheckCircle
    | ChevronsUp
    | ChevronsDown
    | ChevronDown
    | ChevronUp
    | ChevronRight
    | ChevronLeft
    | ArrowDown
    | ArrowUp
    | ArrowLeft
    | Paperclip
    | Send
    | Stop
    | Copy
    | Fork
    | Refresh
    | Trash
    | Pencil
    | Pin
    | Archive
    | Gear
    | Sliders
    | Sun
    | Moon
    | Wrench
    | Server
    | Key
    | Devices
    | TextWrap
    | Download
    | Upload
    | Alert
    | Info
    | PanelLeft
    | Message
    | More
    | Link
    | ExternalLink
    | File
    | Image
    | Clock
    | Sparkle
    | Keyboard
    | LogOut
    | Database

/// Lucide 官方集的语义映射与图标控件（旧 Design/Icons.fs）。
/// 字形几何全部来自官方 `LucideImageExtension`，不自绘路径。画笔与尺寸都由调用方给：
/// 画笔是主题管理者的活实例（官方实现保留同一画笔实例，改色即跟随），尺寸是活的可观察值。
module IconSet =

    /// 全部图标
    let all : Glyph list =
        [ Glyph.Search
          Glyph.Plus
          Glyph.Minus
          Glyph.Close
          Glyph.Check
          Glyph.CheckCircle
          Glyph.ChevronsUp
          Glyph.ChevronsDown
          Glyph.ChevronDown
          Glyph.ChevronUp
          Glyph.ChevronRight
          Glyph.ChevronLeft
          Glyph.ArrowDown
          Glyph.ArrowUp
          Glyph.ArrowLeft
          Glyph.Paperclip
          Glyph.Send
          Glyph.Stop
          Glyph.Copy
          Glyph.Fork
          Glyph.Refresh
          Glyph.Trash
          Glyph.Pencil
          Glyph.Pin
          Glyph.Archive
          Glyph.Gear
          Glyph.Sliders
          Glyph.Sun
          Glyph.Moon
          Glyph.Wrench
          Glyph.Server
          Glyph.Key
          Glyph.Devices
          Glyph.TextWrap
          Glyph.Download
          Glyph.Upload
          Glyph.Alert
          Glyph.Info
          Glyph.PanelLeft
          Glyph.Message
          Glyph.More
          Glyph.Link
          Glyph.ExternalLink
          Glyph.File
          Glyph.Image
          Glyph.Clock
          Glyph.Sparkle
          Glyph.Keyboard
          Glyph.LogOut
          Glyph.Database ]

    /// 语义名对应的 Lucide 字形
    let kind (glyph: Glyph) : PackIconLucideKind =
        match glyph with
        | Glyph.Search -> PackIconLucideKind.Search
        | Glyph.Plus -> PackIconLucideKind.Plus
        | Glyph.Minus -> PackIconLucideKind.Minus
        | Glyph.Close -> PackIconLucideKind.X
        | Glyph.Check -> PackIconLucideKind.Check
        | Glyph.CheckCircle -> PackIconLucideKind.CircleCheck
        | Glyph.ChevronsUp -> PackIconLucideKind.ChevronsUp
        | Glyph.ChevronsDown -> PackIconLucideKind.ChevronsDown
        | Glyph.ChevronDown -> PackIconLucideKind.ChevronDown
        | Glyph.ChevronUp -> PackIconLucideKind.ChevronUp
        | Glyph.ChevronRight -> PackIconLucideKind.ChevronRight
        | Glyph.ChevronLeft -> PackIconLucideKind.ChevronLeft
        | Glyph.ArrowDown -> PackIconLucideKind.ArrowDown
        | Glyph.ArrowUp -> PackIconLucideKind.ArrowUp
        | Glyph.ArrowLeft -> PackIconLucideKind.ArrowLeft
        | Glyph.Paperclip -> PackIconLucideKind.Paperclip
        | Glyph.Send -> PackIconLucideKind.ArrowUpFromLine
        | Glyph.Stop -> PackIconLucideKind.Square
        | Glyph.Copy -> PackIconLucideKind.Copy
        | Glyph.Fork -> PackIconLucideKind.GitFork
        | Glyph.Refresh -> PackIconLucideKind.RefreshCw
        | Glyph.Trash -> PackIconLucideKind.Trash2
        | Glyph.Pencil -> PackIconLucideKind.Pencil
        | Glyph.Pin -> PackIconLucideKind.Pin
        | Glyph.Archive -> PackIconLucideKind.Archive
        | Glyph.Gear -> PackIconLucideKind.Settings2
        | Glyph.Sliders -> PackIconLucideKind.SlidersHorizontal
        | Glyph.Sun -> PackIconLucideKind.Sun
        | Glyph.Moon -> PackIconLucideKind.Moon
        | Glyph.Wrench -> PackIconLucideKind.Wrench
        | Glyph.Server -> PackIconLucideKind.Server
        | Glyph.Key -> PackIconLucideKind.KeyRound
        | Glyph.Devices -> PackIconLucideKind.MonitorSmartphone
        | Glyph.TextWrap -> PackIconLucideKind.TextWrap
        | Glyph.Download -> PackIconLucideKind.Download
        | Glyph.Upload -> PackIconLucideKind.Upload
        | Glyph.Alert -> PackIconLucideKind.CircleAlert
        | Glyph.Info -> PackIconLucideKind.Info
        | Glyph.PanelLeft -> PackIconLucideKind.PanelLeft
        | Glyph.Message -> PackIconLucideKind.MessageSquare
        | Glyph.More -> PackIconLucideKind.Ellipsis
        | Glyph.Link -> PackIconLucideKind.Link
        | Glyph.ExternalLink -> PackIconLucideKind.ExternalLink
        | Glyph.File -> PackIconLucideKind.FileText
        | Glyph.Image -> PackIconLucideKind.Image
        | Glyph.Clock -> PackIconLucideKind.Clock
        | Glyph.Sparkle -> PackIconLucideKind.Sparkles
        | Glyph.Keyboard -> PackIconLucideKind.Keyboard
        | Glyph.LogOut -> PackIconLucideKind.LogOut
        | Glyph.Database -> PackIconLucideKind.Database

    // DrawingImage 每次调用都新建，视图重建时反复请求，因此按 (画笔实例, 字形) 缓存；画笔被回收时缓存跟着回收
    let private cache = ConditionalWeakTable<IBrush, ConcurrentDictionary<PackIconLucideKind, DrawingImage>>()

    /// 字形图像（同一画笔、同一字形返回同一实例）
    let drawing (glyph: Glyph) (brush: IBrush) : IImage =
        let perBrush = cache.GetValue(brush, fun _ -> ConcurrentDictionary<PackIconLucideKind, DrawingImage>())
        perBrush.GetOrAdd(kind glyph, fun k -> LucideImageExtension(Kind = k, Brush = brush).ProvideValue(null) :?> DrawingImage)

    /// 图标控件：只设画笔、宽高（绑到活尺寸）与居中
    let image (glyph: Glyph) (brush: IBrush) (size: IObservable<float>) : Control =
        let image = Image(Source = drawing glyph brush, Stretch = Stretch.Uniform)
        image.Bind(Layoutable.WidthProperty, size) |> ignore
        image.Bind(Layoutable.HeightProperty, size) |> ignore
        image.HorizontalAlignment <- HorizontalAlignment.Center
        image.VerticalAlignment <- VerticalAlignment.Center
        image :> Control
