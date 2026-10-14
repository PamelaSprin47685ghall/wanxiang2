namespace Wanxiang.UI

/// 旧 OverlayHost 入条步骤照抄：toastEntries 存 (文本, 撤下函数)，撤下函数里 RemoveAll 同文
type ToastQueue() =
    let toastCap = 3
    let toastEntries = ResizeArray<string * (unit -> unit)>()
    let dismissed = ResizeArray<string>()
    member _.Showing = toastEntries |> Seq.map fst |> List.ofSeq
    member _.Remove(message: string) = toastEntries.RemoveAll(fun (text, _) -> text = message) |> ignore
    member this.Admit(message: string) : string list =
        dismissed.Clear()
        let remove () =
            dismissed.Add message
            toastEntries.RemoveAll(fun (text, _) -> text = message) |> ignore
        toastEntries
        |> Seq.filter (fun (text, _) -> text = message)
        |> Seq.map snd
        |> Array.ofSeq
        |> Array.iter (fun dismiss -> dismiss ())
        while toastEntries.Count >= toastCap do
            let _, dismissOldest = toastEntries[0]
            dismissOldest ()
        toastEntries.Add(message, remove)
        List.ofSeq dismissed
