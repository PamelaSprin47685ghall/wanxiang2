using System.Runtime.InteropServices.JavaScript;

namespace Wanxiang.Interop;

/// <summary>决策 452：localStorage 里的小型本机文本（PWA 的界面偏好等；从旧 BrowserBridge 拆出）。</summary>
public static partial class BrowserLocalStore
{
    /// <summary>读取；无则 null。</summary>
    [JSImport("wxLocalGet", "wanxiang")]
    public static partial string? Get(string key);

    /// <summary>写入。</summary>
    [JSImport("wxLocalSet", "wanxiang")]
    public static partial void Set(string key, string value);
}
