namespace Wanxiang.UI

// 照抄旧 Views/ChatView.fs SetBreadcrumb（控件写入换成记录）、BreadcrumbText、ActivateBreadcrumb
type BreadcrumbView = { visible: bool; text: string; clickable: bool; description: string }

type BreadcrumbAction<'Id>() =
    let mutable breadcrumbTarget: 'Id option = None
    let mutable barVisible = false
    let mutable breadcrumbText = ""
    let mutable focusable = false
    let mutable tip = ""
    let snapshot () = { visible = barVisible; text = breadcrumbText; clickable = focusable; description = tip }

    member _.Set(crumb: (string * string * bool * 'Id) option) =
        match crumb with
        | None ->
            breadcrumbTarget <- None
            barVisible <- false
        | Some(text, description, parentAvailable, parentId) ->
            breadcrumbText <- text
            breadcrumbTarget <- if parentAvailable then Some parentId else None
            focusable <- parentAvailable
            tip <- description
            barVisible <- true
        snapshot ()

    member _.View = snapshot ()
    member _.Text = if barVisible then breadcrumbText else ""
    member _.Activate() =
        match breadcrumbTarget with
        | Some id -> Some id
        | None -> None
