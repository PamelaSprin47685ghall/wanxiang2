namespace Wanxiang.UI

/// 决策 251：外部打开的 `wanxiang://` 链接的收件箱。
/// 启动参数、第二个实例转发、macOS 系统激活都投到这里；MainView 订阅后在 UI 线程打开。
/// MainView 还没起来时先排队，订阅时一次交付。排队与交付规则归 LinkInbox。
module ExternalLinks =
    /// 进程内唯一的收件箱（Program 在 MainView 起来之前就会投递）。
    let inbox = Inbox<string>()

    let post (url: string) = LinkInbox.post inbox url

    /// 订阅（MainView 构造时）。返回此前排队的链接已交付。
    let subscribe (handler: string -> unit) = inbox.Subscribe handler
