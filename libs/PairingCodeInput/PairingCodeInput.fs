namespace Wanxiang.UI

open System

/// 旧 Views/Dialogs.fs 连接对话框的配对码输入：任意 Unicode 数字归一为 ASCII，最多 6 位
module PairingCodeInput =

    let normalize (raw: string) =
        (if isNull raw then "" else raw)
        |> Seq.filter Char.IsDigit
        |> Seq.map (fun ch -> char (int '0' + int (Char.GetNumericValue ch)))
        |> Seq.truncate 6
        |> Seq.toArray
        |> String

    /// 有数字时加字距
    let letterSpacing (digits: string) = if digits.Length = 0 then 0.0 else 8.0

    /// 提交时码不足 6 位的提示
    let incomplete = "请输入 6 位配对码。"

/// 同一串 6 位码只自动提交一次（布局重排会重放同一文本），改动后才可再次触发
type PairingAutoSubmit() =
    let mutable submitted = ""

    /// 输入了归一后的 digits；返回 true 表示要提交
    member _.Changed(digits: string) =
        if digits.Length = 6 then
            if digits <> submitted then
                submitted <- digits
                true
            else
                false
        else
            submitted <- ""
            false
