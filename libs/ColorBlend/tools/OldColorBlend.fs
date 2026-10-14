namespace Wanxiang.UI

/// 旧 Interaction.blendOver 的分量计算照抄（画笔换成 ARGB 四元组）
module ColorBlend =
    let over (bA: byte, bR: byte, bG: byte, bB: byte) (oA: byte, oR: byte, oG: byte, oB: byte) =
        let blend (bc: byte) (oc: byte) (alpha: byte) =
            byte (int bc + (int oc - int bc) * int alpha / 255)
        bA, blend bR oR oA, blend bG oG oA, blend bB oB oA
