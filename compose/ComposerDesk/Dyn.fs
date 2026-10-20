namespace Wanxiang2.ComposerDesk

open System
open Avalonia.Markup.Xaml.MarkupExtensions

/// 动态资源绑定辅助：主题管理者把活值写进 ResourceDictionary，控件绑定即随主题资源生效。
/// 本项目的外观一律走主题资源键，不再有外观记录与手工订阅。
module internal Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp
