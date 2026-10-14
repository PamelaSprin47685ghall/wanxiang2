namespace Wanxiang2.__NAME__

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media

/// 本控件的外观。所有样式值都由主题管理者提供活的可变实例；本库不写任何字面样式值。
type __NAME__Look =
    { Background: IBrush
      Foreground: IBrush
      /// 尺寸也是活的：管理者改值，控件跟着变。
      Padding: IObservable<Thickness> }

/// 控件自己拥有状态与行为；外部只经方法和事件与它合作。
type __NAME__(look: __NAME__Look) as this =
    inherit Border()
    let label = TextBlock(Foreground = look.Foreground)
    let mutable count = 0
    let changed = Event<int>()
    do
        this.Background <- look.Background
        this.Child <- label
        this.Bind(Border.PaddingProperty, look.Padding) |> ignore
        label.Text <- "0"

    /// 计数变了。
    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Count = count

    member _.Bump() =
        count <- count + 1
        label.Text <- string count
        changed.Trigger count
