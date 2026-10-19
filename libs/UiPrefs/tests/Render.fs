module Render

open System.Text
open Wanxiang.UI

let show (p: UiPrefs) =
    sprintf "theme=%s font=%g auto=%b wrap=%b side=%g coll=%b arch=%b motion=%b enter=%b drafts=%b win=%gx%g pos=%d,%d has=%b"
        (ThemePreference.toText p.theme) p.fontScale p.autoCollapseReasoning p.codeWrap p.sidebarWidth p.sidebarCollapsed
        p.showArchived p.reduceMotion p.enterSends p.persistDrafts p.windowWidth p.windowHeight p.windowX p.windowY p.hasWindowPosition

/// 每个输入：读出的偏好、改一处后写回的文本。
let all (load: string option -> UiPrefs) (save: string option -> UiPrefs -> string) (inputs: string list) =
    let sb = StringBuilder()
    for raw in inputs do
        let text = if raw = "<missing>" then None else Some raw
        let p = load text
        sb.AppendLine("<< " + raw).AppendLine(show p) |> ignore
        sb.AppendLine(save text { p with theme = AlwaysDark; windowX = 3 }) |> ignore
    for t in [ FollowSystem; AlwaysLight; AlwaysDark ] do
        sb.AppendLine(sprintf "%s %s %s" (ThemePreference.toText t) (ThemePreference.label t) (show { (load None) with theme = ThemePreference.ofText (ThemePreference.toText t) })) |> ignore
    sb.ToString()
