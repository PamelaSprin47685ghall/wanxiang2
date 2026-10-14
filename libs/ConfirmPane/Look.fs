namespace Wanxiang2.ConfirmPane

open System
open Avalonia.Controls
open Avalonia.Media

/// 外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type ConfirmPaneLook =
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
      /// 正文行高
      BodyLineHeight: IObservable<float>
    }
