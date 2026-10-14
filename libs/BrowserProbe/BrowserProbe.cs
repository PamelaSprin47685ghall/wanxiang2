using System.Runtime.InteropServices.JavaScript;

namespace Wanxiang.Interop;

/// <summary>决策 450：一致性探针（页面地址带 wxprobe=1 时打开，默认关闭；从旧 BrowserBridge 拆出）。</summary>
public static partial class BrowserProbe
{
    /// <summary>探针是否打开。</summary>
    [JSImport("wxProbeEnabled", "wanxiang")]
    public static partial bool Enabled();

    /// <summary>记录一次探点命中（写入 globalThis.__wxProbe）。</summary>
    [JSImport("wxProbe", "wanxiang")]
    public static partial void Hit(string name);
}
