namespace Wanxiang.UI

open System

module DurationText =

    let format (ms: int64) =
        if ms >= 1000L then sprintf "%.1fs" (float ms / 1000.0)
        elif ms > 0L then sprintf "%dms" (int ms)
        else ""
