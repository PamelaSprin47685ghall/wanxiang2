namespace Wanxiang2.ListEmptyCard

open System
open Avalonia.Controls
open Avalonia.Media

/// 外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type ListEmptyCardLook =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      Accent: IBrush
      Danger: IBrush
      Success: IBrush
      Warning: IBrush
      /// 输入壳的底与描边
      FieldSurface: IBrush
      FieldEdge: IBrush
      BodyFontSize: IObservable<float>
      SmallFontSize: IObservable<float>
      CaptionFontSize: IObservable<float>
      MicroFontSize: IObservable<float>
      HeadingFontSize: IObservable<float>
      TitleFontSize: IObservable<float>
      DisplayFontSize: IObservable<float>
      CaptionLineHeight: IObservable<float>
      /// 字段标签的字距
      LabelTracking: IObservable<float>
      SpaceXs: IObservable<float>
      SpaceSm: IObservable<float>
      SpaceMd: IObservable<float>
      SpaceXl: IObservable<float>
      Space2xl: IObservable<float>
      Space4xl: IObservable<float>
      RadiusMd: IObservable<float>
      RadiusLg: IObservable<float>
      BorderWidth: IObservable<float>
      FieldInsetX: IObservable<float>
      TextFieldPaddingY: IObservable<float>
      TextFieldTextMinHeight: IObservable<float>
      SettingsRowMinHeight: IObservable<float>
      MonoFont: IObservable<FontFamily>
      /// 图标槽：名字 → 笔 → 图标控件
      Glyph: string -> IBrush -> Control
      /// 品牌标（宿主给图形；参数是尺寸）
      Logo: float -> Control
      LogoSize: IObservable<float>
      /// 骨架条：底、透明度、高、圆角
      SkeletonFill: IBrush
      SkeletonOpacity: IObservable<float>
      SkeletonBarHeight: IObservable<float>
      RadiusSm: IObservable<float>
      Space3xl: IObservable<float>
      Space6xl: IObservable<float>
    }
