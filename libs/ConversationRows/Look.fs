namespace Wanxiang2.ConversationRows

open System
open Avalonia.Controls
open Avalonia.Media

/// 外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type ConversationRowsLook =
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
      /// 当前 / 已选行的底，悬停底，已选行上的悬停底，命中词底
      Selected: IBrush
      Hover: IBrush
      SelectedHover: IBrush
      AccentSoft: IBrush
      RowMinHeight: IObservable<float>
      RowPaddingY: IObservable<float>
      StateSlotWidth: IObservable<float>
      StateGlyphSize: IObservable<float>
      RunningDotSize: IObservable<float>
      SelectedEdgeWidth: IObservable<float>
      RowContentSpacing: IObservable<float>
      /// 行内「更多操作」命中面：桌面 / 紧凑
      IconButtonSize: IObservable<float>
      CompactTarget: IObservable<float>
      /// 常驻淡显的不透明度
      OpacitySubtle: IObservable<float>
      /// 行底色补间（参数：是否连描边一起补间）
      Transitions: bool -> Avalonia.Animation.Transitions
    }
