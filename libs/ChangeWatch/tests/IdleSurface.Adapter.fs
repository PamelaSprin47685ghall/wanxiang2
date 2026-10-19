module IdleSurfaceAdapter
open Wanxiang.UI

/// 新库一侧：把决定写成与旧分支相同的 trace
let run a e r h c o =
    let v = IdleSurface.decide a e r h c o
    let state =
        match v.empty with
        | IdleEmpty.ConnectionFailed err -> sprintf "ConnectionFailed %s" err
        | other -> sprintf "%A" other
    String.concat "; "
        [ sprintf "empty=%s primary=%A" state v.primary
          sprintf "enabled=%b reason=%s" v.composerEnabled v.composerReason
          sprintf "model=%s" (defaultArg v.modelLabel "<selection>") ]

let openComposer a = IdleSurface.openComposer a
