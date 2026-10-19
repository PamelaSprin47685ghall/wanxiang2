module StartReadyAdapter
open Wanxiang.UI

/// 新库一侧：两个 option 折成 ActiveConversation
let isReady a c g e (activeConvId: int option) (view: int option) =
    let active =
        match activeConvId, view with
        | None, _ -> ActiveConversation.None
        | Some _, Some n -> ActiveConversation.Loaded n
        | Some _, None -> ActiveConversation.Loading
    StartReady.isReady a c g e active
