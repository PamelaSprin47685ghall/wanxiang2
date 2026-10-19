using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Wanxiang.Interop;

/// <summary>PWA 的本地草稿快照（IndexedDB 单条记录；W5：只存本机，不上传；从旧 BrowserBridge 拆出）。</summary>
public static partial class BrowserDrafts
{
    /// <summary>草稿快照（JSON 字符串；无则空串）。</summary>
    [JSImport("draftGet", "wanxiang")]
    public static partial Task<string> Get();

    /// <summary>保存草稿快照。</summary>
    [JSImport("draftPut", "wanxiang")]
    public static partial Task Put(string json);

    /// <summary>删除草稿快照。</summary>
    [JSImport("draftDelete", "wanxiang")]
    public static partial Task Delete();
}
