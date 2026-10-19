namespace Wanxiang.UI

open System

module GenerationErrorTitle =

    /// 错误标题必须是人话：兼容路径（旧主机纯文本 error）可能把原始异常直接塞进 message；
    /// 含换行、堆栈帧或异常类名的原文一律降级为固定标题，原文只进按需展开的「技术细节」。
    let ofMessage (message: string) : string =
        let m = if isNull message then "" else message.Trim()
        let looksRaw =
            m.Contains "\n" || m.Contains "Exception" || m.Contains "StackTrace"
            || m.Contains " at " || m.StartsWith "error:"
        if String.IsNullOrWhiteSpace m || looksRaw then "生成失败"
        else m
