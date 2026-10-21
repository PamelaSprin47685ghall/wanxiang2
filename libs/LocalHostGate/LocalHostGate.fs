namespace Wanxiang.UI

/// 决策 291：同进程主机的就绪门。桌面入口在后台重放日志、启动主机时把它换成那个任务；
/// 客户端第一次连接前等它完成。默认已完成（PWA、纯客户端、测试）。
/// 迁自旧 Model/StartupTrace.fs；独立 fsproj：libs/StartupTrace/StartupTrace.fsproj 已存在且不可改，故新建。
module LocalHostGate =
    let mutable ready: System.Threading.Tasks.Task =
        System.Threading.Tasks.Task.CompletedTask
