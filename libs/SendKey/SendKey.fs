namespace Wanxiang.UI

/// 回车发送还是换行。
module SendKey =

    /// 发送键与换行键严格互补，不交叉：Enter 发送模式下 Ctrl（或 ⌘）+Enter 换行，
    /// Ctrl+Enter 发送模式下裸 Enter 换行；Shift+Enter 永远换行。
    /// 输入法组合中的回车归输入法，由调用方先行排除。
    let sends (enterSends: bool) (shift: bool) (ctrl: bool) : bool =
        not shift && (enterSends <> ctrl)
