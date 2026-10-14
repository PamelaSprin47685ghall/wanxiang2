using System;
using System.Runtime.InteropServices.JavaScript;

namespace Wanxiang.Interop;

/// <summary>PWA 所在页面的状态与生命周期（从旧 BrowserBridge 拆出）。</summary>
public static partial class BrowserPage
{
    /// <summary>当前页面 URL（用于推导同源 ws(s) 连接地址）。</summary>
    [JSImport("pageUrl", "wanxiang")]
    public static partial string Url();

    /// <summary>CSS 像素视口宽（响应式断点唯一依据；Bounds.Width 在 Browser 高 DPR 下不可靠）。</summary>
    [JSImport("wxViewportWidth", "wanxiang")]
    public static partial double ViewportWidth();

    /// <summary>网络恢复 / 页面回到前台的计数（第 213 节）：数值变化即“现在值得立刻重连”，
    /// 由 F# 侧轮询比较，跳过正在等待的退避延时。</summary>
    [JSImport("wxResumeEpoch", "wanxiang")]
    public static partial int ResumeEpoch();

    /// <summary>第一次完整对话成功结束：PWA 安装引导此后才允许出现（第 210 节）。</summary>
    [JSImport("wxConversationSucceeded", "wanxiang")]
    public static partial void ConversationSucceeded();

    /// <summary>决策 451：页面转入后台 / 即将卸载（桌面的 Window.Closing 对应物）。</summary>
    public static event Action? Hidden;

    /// <summary>由 main.js 调用：getAssemblyExports("BrowserPage.dll") 后的 Wanxiang.Interop.BrowserPage.NotifyHidden。</summary>
    [JSExport]
    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    public static void NotifyHidden() => Hidden?.Invoke();
}
