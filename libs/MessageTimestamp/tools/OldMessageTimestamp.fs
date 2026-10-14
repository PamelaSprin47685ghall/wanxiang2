namespace Wanxiang.UI

open System

module MessageTimestamp =

    /// 消息时间的展示形式：今天只给时刻，昨天加前缀，更早给日期。
    /// 聊天里绝大多数消息是今天的，堆完整日期只是噪音。
    let format (now: DateTimeOffset) (at: DateTimeOffset) : string =
        let local = at
        let today = now.Date
        let days = (today - local.Date).TotalDays
        if days < 1.0 then local.ToString "HH:mm"
        elif days < 2.0 then local.ToString "'昨天' HH:mm"
        elif local.Year = now.Year then local.ToString "M月d日 HH:mm"
        else local.ToString "yyyy年M月d日 HH:mm"
