namespace Wanxiang2.GeneralPane

open System
open Avalonia.Controls
open Avalonia.Media

/// 外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type GeneralPaneLook =
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
      SpaceXXs: IObservable<float>
      /// 多行指令框的最小高
      TextAreaMinHeight: IObservable<float>
      /// 两列表单退化为单列的断点
      FormSingleColumnBreakpoint: IObservable<float>
      /// 关于页键名列、字号值的最小宽
      AboutKeyMinWidth: IObservable<float>
      FontSizeValueMinWidth: IObservable<float>
      /// 异步提交键的最小宽
      PendingMinWidth: IObservable<float>
      /// 下拉选择器：文字最宽、纵向内边距
      SelectCaptionMaxWidth: IObservable<float>
      SelectPaddingY: IObservable<float>
      /// 可选文字的选区底色
      Selection: IBrush
    }
