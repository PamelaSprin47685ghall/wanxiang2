namespace Wanxiang.UI

/// 耗时角标：一秒以上保留一位小数的秒，以下给毫秒，零或负数不显示。
module DurationText =

    let format (ms: int64) =
        if ms >= 1000L then sprintf "%.1fs" (float ms / 1000.0)
        elif ms > 0L then sprintf "%dms" (int ms)
        else ""
