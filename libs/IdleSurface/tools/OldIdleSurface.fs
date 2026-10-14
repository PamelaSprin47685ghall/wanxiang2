namespace Wanxiang.UI

/// 旧 AppShell Refresh 的「没有打开对话」分支照抄：控件调用换成记录到 trace
module IdleSurface =
    let run (authenticated: bool) (connectionError: string option) (rePair: bool) (hasSavedCredentials: bool) (catalogReady: bool) (onboardingBusy: bool) =
        let trace = ResizeArray<string>()
        let showEmpty (state: string) (primary: string option) = trace.Add(sprintf "empty=%s primary=%A" state primary)
        let setEnabled (enabled: bool, reason: string) = trace.Add(sprintf "enabled=%b reason=%s" enabled reason)
        let setModelLabel (label: string) = trace.Add(sprintf "model=%s" label)
        if not authenticated then
            match connectionError with
            | Some err ->
                let entryLabel =
                    match (if rePair then Some "RePair" else None) with
                    | Some "RePair" -> "重新配对"
                    | _ -> "重新连接"
                showEmpty (sprintf "ConnectionFailed %s" err) (Some entryLabel)
                setEnabled(false, sprintf "连接失败：%s" err)
                setModelLabel "未连接"
            | None ->
                if not hasSavedCredentials then
                    showEmpty "NotConnected" (Some "连接主机")
                    setEnabled(false, "可以先写草稿，连上主机后再发送。")
                    setModelLabel "未连接"
                else
                    showEmpty "NotConnected" (Some "连接主机")
                    setEnabled(false, "正在连接主机…草稿已保留。")
                    setModelLabel "正在连接"
        elif not catalogReady || onboardingBusy then
            showEmpty "Onboarding" None
            setEnabled(false, "先在上方添加一个模型服务，就能在这里输入。")
            setModelLabel "未配置模型"
        else
            showEmpty "NoConversation" None
            setEnabled(true, "")
            setModelLabel "<selection>"
        String.concat "; " trace
    let openComposer (authenticated: bool) = authenticated, (if authenticated then "" else "连接已断开，重新连接后可继续发送。")

module Adapter =
    let run a e r h c o = IdleSurface.run a e r h c o
    let openComposer a = IdleSurface.openComposer a
