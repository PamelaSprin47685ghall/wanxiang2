namespace Wanxiang2.SettingsFrame

open System
open Avalonia.Controls
open Avalonia.Media

/// 外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type SettingsFrameLook =
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
      Space6xl: IObservable<float>
      Space8xl: IObservable<float>
      /// 页面底、导航轴底、chrome 分隔线、选中底
      Canvas: IBrush
      Rail: IBrush
      Chrome: IBrush
      Selected: IBrush
      /// 头部高、导航列宽、导航键最小高、选中左缘宽、内容最宽
      BarHeight: IObservable<float>
      NavWidth: IObservable<float>
      NavMinHeight: IObservable<float>
      SelectedEdgeWidth: IObservable<float>
      ContentMaxWidth: IObservable<float>
      /// 未选中图标的不透明度
      SubtleOpacity: IObservable<float>
      /// 窄屏断点
      CompactBreakpoint: IObservable<float>
    }
