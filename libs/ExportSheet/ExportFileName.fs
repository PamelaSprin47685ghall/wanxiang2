namespace Wanxiang.UI

open System

/// 导出文件名（旧 Views/Brand.fs 的 Export.safeFileName）。
module ExportFileName =

    /// 标题不是路径；不同平台统一处理分隔符、控制字符和过长文件名。
    let ofTitle (title: string) =
        let source = if String.IsNullOrWhiteSpace title then "对话" else title

        let cleaned =
            source
            |> Seq.map (fun c ->
                if Char.IsControl c || "<>:\"/\\|?*".Contains c then
                    '_'
                else
                    c)
            |> Seq.toArray
            |> String

        let trimmed = cleaned.Trim().Trim('.')
        let trimmed = if String.IsNullOrWhiteSpace trimmed then "对话" else trimmed
        let elements = Globalization.StringInfo.GetTextElementEnumerator trimmed
        let builder = Text.StringBuilder()
        let mutable bytes = 0
        let mutable doneName = false

        while not doneName && elements.MoveNext() do
            let element = elements.GetTextElement()
            let size = Text.Encoding.UTF8.GetByteCount element

            if bytes + size > 220 then
                doneName <- true
            else
                builder.Append element |> ignore
                bytes <- bytes + size

        let bounded = if builder.Length = 0 then "对话" else builder.ToString()
        bounded + ".md"
