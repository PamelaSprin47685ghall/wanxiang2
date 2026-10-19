namespace Wanxiang2.ChatPane

open System
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.ChatScroller
open Wanxiang2.ExchangeNav
open Wanxiang2.ChatEmpty
open Wanxiang2.SkeletonList

/// 对话区外观参数，由主题管理者填
type ChatPaneMetrics =
    { Space3xl: IObservable<float>
      /// 起步模式空态留白（旧 Spacing.space2xl）
      Space2xl: IObservable<float>
      /// 空态品牌标边长（旧 Tokens.logoEmpty；构造参数，取当前值）
      LogoEmpty: LiveValue<float> }

module ChatPaneMetrics =

    let ofTheme (manager: ThemeManager) (_styles: ThemeStyles) : ChatPaneMetrics =
        let space s = manager.Space s :> IObservable<float>
        { Space2xl = space Space.X2l
          Space3xl = space Space.X3l
          LogoEmpty = manager.Metric Metric.LogoEmpty }
