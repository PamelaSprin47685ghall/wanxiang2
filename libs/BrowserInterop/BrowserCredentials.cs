using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Wanxiang.Interop;

/// <summary>
/// PWA 的连接凭据（IndexedDB，由 wwwroot/main.js 以 setModuleImports("wanxiang", …) 提供；从旧 BrowserBridge 拆出）。
/// 只在浏览器里可调用；桌面端以 OperatingSystem.IsBrowser() 分流，不会走到这里。
/// </summary>
public static partial class BrowserCredentials
{
    /// <summary>全部连接记录（JSON 字符串，按 updatedAt 降序；Q191：instanceId 为主键）。</summary>
    [JSImport("credList", "wanxiang")]
    public static partial Task<string> List();

    /// <summary>保存/更新一条连接凭据。</summary>
    [JSImport("credPut", "wanxiang")]
    public static partial Task Put(string instanceId, string url, string token, string name);

    /// <summary>删除一条连接凭据。</summary>
    [JSImport("credDelete", "wanxiang")]
    public static partial Task Delete(string instanceId);
}
