namespace Wanxiang2.FocusOrigin

open Avalonia.Controls
open Avalonia.Input

/// 聚焦来源（迁自旧 Controls/Interaction.fs 的 reportFocusOrigin）：键盘与程序化聚焦挂类名
/// `focus-by-program`，指针聚焦与失焦去掉它。视觉（焦点环）由主题的样式表按类名决定。
module FocusOrigin =
    /// 主题样式表用的类名。
    [<Literal>]
    let ProgrammaticClass = "focus-by-program"

    let report (control: Control) =
        control.GotFocus.Add(fun e ->
            match e.NavigationMethod with
            | NavigationMethod.Pointer -> control.Classes.Remove ProgrammaticClass |> ignore
            | _ -> control.Classes.Add ProgrammaticClass |> ignore)
        control.LostFocus.Add(fun _ -> control.Classes.Remove ProgrammaticClass |> ignore)
