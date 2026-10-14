using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Wanxiang.Interop;

/// <summary>决策 342：PWA 里用浏览器原生解码 + (Offscreen)Canvas 缩小图片，不占 wasm 单线程（从旧 BrowserBridge 拆出）。</summary>
public static partial class BrowserImage
{
    /// <summary>返回 JSON：{ok:true,b64,mediaType,width,height,originalWidth,originalHeight} 或 {ok:false,error}。</summary>
    [JSImport("imageShrink", "wanxiang")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    public static partial Task<string> Shrink(
        [JSMarshalAs<JSType.Array<JSType.Number>>] byte[] bytes, string mediaType, int maxDimension, double maxBytes);
}
