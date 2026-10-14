namespace Wanxiang.UI

/// 半透明颜色按 alpha 叠到底色上（旧 Controls/Interaction.fs 的 blendOver；Avalonia 没有原生画笔叠加）。
module ColorBlend =

    /// 颜色为 (A, R, G, B)。结果保留底色的 A；各分量按叠加色的 A 线性插值，整数除法向零取整。
    let over (bA: byte, bR: byte, bG: byte, bB: byte) (oA: byte, oR: byte, oG: byte, oB: byte) : byte * byte * byte * byte =
        let blend (bc: byte) (oc: byte) = byte (int bc + (int oc - int bc) * int oA / 255)
        bA, blend bR oR, blend bG oG, blend bB oB
