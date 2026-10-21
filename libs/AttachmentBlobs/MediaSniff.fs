namespace Wanxiang.Server

open System

/// 文件头嗅探和文件名清理。不碰文件系统。
module MediaSniff =

    /// 按前 512 字节内的魔数判断类型；都不是时，全是可见字符或空白就算 text/plain，否则 None。
    let sniff (head: byte[]) : string option =
        let hasMagic (bytes: byte[]) =
            head.Length >= bytes.Length
            && Array.forall2 (=) bytes head[0 .. bytes.Length - 1]

        if hasMagic [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |] then
            Some "image/png"
        elif hasMagic [| 0xFFuy; 0xD8uy; 0xFFuy |] then
            Some "image/jpeg"
        elif hasMagic (Text.Encoding.ASCII.GetBytes "GIF8") then
            Some "image/gif"
        elif hasMagic (Text.Encoding.ASCII.GetBytes "%PDF") then
            Some "application/pdf"
        elif hasMagic [| 0x50uy; 0x4Buy; 0x03uy; 0x04uy |] then
            Some "application/zip"
        else
            let mutable textLike = true
            let mutable i = 0

            while textLike && i < head.Length do
                let b = head[i]

                if b < 0x09uy || (b > 0x0Duy && b < 0x20uy) then
                    textLike <- false

                i <- i + 1

            if textLike then Some "text/plain" else None

    /// 声明类型为空或 application/octet-stream 时采用嗅探结果；嗅探不出来就保留声明值（空则 octet-stream）。
    let resolve (declared: string) (sniffed: string option) : string =
        if String.IsNullOrWhiteSpace declared || declared = "application/octet-stream" then
            sniffed
            |> Option.defaultValue (
                if String.IsNullOrWhiteSpace declared then
                    "application/octet-stream"
                else
                    declared
            )
        else
            declared

    /// 去掉控制字符，最多 255 个字符，去掉首尾空白。只用于显示，绝不用作存储路径。
    let sanitizeFileName (name: string) : string =
        let cleaned =
            name
            |> Seq.filter (fun c -> c >= ' ')
            |> Seq.truncate 255
            |> Seq.map string
            |> String.concat ""

        cleaned.Trim()
