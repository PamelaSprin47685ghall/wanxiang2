namespace Wanxiang2.ConversationRows

open System

/// 本库共享原语的别名（Tone/TagTone 的名字按旧实现不变）；原语与行为归 libs/UiKit。
type internal Tone = Wanxiang2.UiKit.Tone

type internal TagTone = Wanxiang2.UiKit.TagTone

/// 可点 Border（旧 Kit.fs 里的 Tap 现在归 UiKit）
type internal Tap = Wanxiang2.UiKit.Tap

/// 本库的 Kit：把本库 Look 的活值映到共享的 UiKit.KitLook；原语与行为归 libs/UiKit。
type internal Kit(look: ConversationRowsLook) =
    inherit Wanxiang2.UiKit.Kit(
        { Text = look.Text
          Muted = look.Muted
          Faint = look.Faint
          Accent = look.Accent
          FieldSurface = look.FieldSurface
          FieldEdge = look.FieldEdge
          BodyFontSize = look.BodyFontSize
          CaptionFontSize = look.CaptionFontSize
          MicroFontSize = look.MicroFontSize
          HeadingFontSize = look.HeadingFontSize
          TitleFontSize = look.TitleFontSize
          CaptionLineHeight = look.CaptionLineHeight
          LabelTracking = look.LabelTracking
          SpaceXs = look.SpaceXs
          SpaceSm = look.SpaceSm
          SpaceMd = look.SpaceMd
          SpaceXl = look.SpaceXl
          RadiusMd = look.RadiusMd
          RadiusLg = look.RadiusLg
          BorderWidth = look.BorderWidth
          FieldInsetX = look.FieldInsetX
          TextFieldPaddingY = look.TextFieldPaddingY
          TextFieldTextMinHeight = look.TextFieldTextMinHeight
          SettingsRowMinHeight = look.SettingsRowMinHeight
          Glyph = look.Glyph })

    /// 两个活值合成一个（UiKit.Kit.Pair 的转发：静态成员不经派生类型名解析）
    static member Pair(a: IObservable<float>, b: IObservable<float>) = Wanxiang2.UiKit.Kit.Pair(a, b)
