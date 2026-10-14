namespace Wanxiang.UI

/// 旧 Composer 回车分支照抄：返回 true 表示这次回车发送
module SendKey =
    let sends (enterSends: bool) (shift: bool) (ctrl: bool) =
        let mutable submitted = false
        if shift then ()
        elif (enterSends && not ctrl) || (not enterSends && ctrl) then
            submitted <- true
        submitted
