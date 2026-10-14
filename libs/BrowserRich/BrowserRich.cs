using System.Runtime.InteropServices.JavaScript;

namespace Wanxiang.Interop;

/// <summary>浏览器自带 JS 引擎里的 highlight.js / KaTeX（从旧 BrowserBridge 拆出）。脚本正文由 .NET 侧给出。</summary>
public static partial class BrowserRich
{
    /// <summary>在全局作用域执行一段脚本（highlight.js / KaTeX 的加载）。</summary>
    [JSImport("richLoad", "wanxiang")]
    public static partial void Load(string source);

    /// <summary>highlight.js 着色，返回带 span 的 HTML；失败返回空串。</summary>
    [JSImport("richHighlight", "wanxiang")]
    public static partial string Highlight(string code, string language);

    /// <summary>KaTeX 排版，返回 HTML；失败返回空串。</summary>
    [JSImport("richMath", "wanxiang")]
    public static partial string Math(string tex, bool displayMode);
}
