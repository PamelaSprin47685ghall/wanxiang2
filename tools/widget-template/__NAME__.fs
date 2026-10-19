namespace Wanxiang2.__NAME__

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media

/// 本控件的展示事实（单入口输入）。只放语义内容，不放颜色/字号/间距——
/// 外观值由 compose/Theme 的资源字典与样式类下发（根 AGENTS.md《界面与前端组装（UI）》规则 2）。
type __NAME__VS =
    { Visible: bool
      Text: string }

/// 控件自己拥有状态与行为；对外只有幂等的 `Render` 单入口与意图事件出口。
/// 不声明 `*Look`、不用 Follow 逐属性订阅、不开 DispatcherTimer、不散落 Dispatcher.UIThread.Post。
type __NAME__() as this =
    inherit Border()
    let label = TextBlock()
    let mutable count = 0
    let changed = Event<int>()
    do
        this.Classes.Add "grouping-card"
        this.Child <- label
        label.Text <- "0"

    /// 计数变了。
    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Count = count

    /// 单入口幂等投影：一次性把 VS 全部落到视觉属性。
    member _.Render(vs: __NAME__VS) =
        this.IsVisible <- vs.Visible
        label.Text <- if String.IsNullOrEmpty vs.Text then string count else vs.Text

    /// 用户意图上报（点击即报数，跨组件副作用归组合者）。
    member _.Bump() =
        count <- count + 1
        label.Text <- string count
        changed.Trigger count
