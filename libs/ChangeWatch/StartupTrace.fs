namespace Wanxiang.UI

open System.Collections.Generic

/// 决策 290：启动感知性能打点，迁自旧 `Model/StartupTrace.fs`。
/// 只在环境变量 WANXIANG_STARTUP_TRACE 存在时输出；每个里程碑只记一次，单位是「距进程启动」的毫秒。
///   firstFrame   主窗口第一帧画出
///   listReady    对话列表快照到达并渲染
///   chatReady    当前对话快照到达并渲染
/// 值为 "exit" 时在 chatReady（或 listReady 后 3 秒无对话）后退出进程，供 tools/perf_desktop.sh 采样。
/// 读环境变量、取进程启动时间、写 stderr、退出进程归宿主；这里给出模式解析、去重与输出行。
module StartupTrace =

    /// 由环境变量的值得到 (是否打点, 是否采样后退出)。
    let modeOf (value: string) : bool * bool =
        not (System.String.IsNullOrEmpty value), value = "exit"

    /// 一行输出（写到 stderr）。
    let line (name: string) (uptimeMs: float) =
        sprintf "startup.%s uptime_ms=%.0f" name uptimeMs

    // ---- 执行侧（迁自旧 Model/StartupTrace.fs；入口外包后宿主就是本仓库，2026-10-17 增补）----
    // 环境变量、进程启动时间与 stderr 在这里持有；入口（app/Desktop / app/Pwa）只安装 requestExit。
    // 默认零开销：一次环境变量读取；每个里程碑只记一次（去重集合，等价旧 StartupMilestones.Mark）。
    let private mode =
        try
            System.Environment.GetEnvironmentVariable "WANXIANG_STARTUP_TRACE"
        with _ ->
            null

    let enabled, exitAfter = modeOf mode

    let private seen = System.Collections.Generic.HashSet<string>()

    let private origin =
        lazy
            (try
                System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()
             with _ ->
                 System.DateTime.UtcNow)

    /// 记录里程碑；返回是否首次记录。
    let mark (name: string) : bool =
        if not enabled then
            false
        elif seen.Add name then
            eprintfn "%s" (line name (System.DateTime.UtcNow - origin.Force()).TotalMilliseconds)
            true
        else
            false

    /// 退出回调由宿主（桌面窗口）安装。
    let mutable requestExit: unit -> unit = ignore

/// 里程碑去重：每个名字只记一次。
type StartupMilestones() =
    let seen = HashSet<string>()

    /// 返回是否首次记录。
    member _.Mark(name: string) : bool = lock seen (fun () -> seen.Add name)
