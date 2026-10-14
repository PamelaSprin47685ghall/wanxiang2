namespace Wanxiang.UI

/// 面包屑的呈现
type BreadcrumbView = { visible: bool; text: string; clickable: bool; description: string }

/// 旧 Views/ChatView.fs SetBreadcrumb / BreadcrumbText / ActivateBreadcrumb 的状态。'Id 为对话标识（旧 Guid）
type BreadcrumbAction<'Id>() =
    let mutable target: 'Id option = None
    let mutable view = { visible = false; text = ""; clickable = false; description = "" }

    /// None 隐藏整条；父对话不可用时显示为纯文字
    member _.Set(crumb: (string * string * bool * 'Id) option) =
        match crumb with
        | None ->
            target <- None
            view <- { view with visible = false }
        | Some(text, description, parentAvailable, parentId) ->
            target <- if parentAvailable then Some parentId else None
            view <- { visible = true; text = text; clickable = parentAvailable; description = description }
        view

    member _.View = view

    /// 显示中的文字；隐藏时为空串
    member _.Text = if view.visible then view.text else ""

    /// 要回到的父对话；不可点时 None
    member _.Activate() = target
