namespace Wanxiang.UI

// 照抄旧 Views/Dialogs.fs codeBox.TextChanged 的归一与自动提交分支、submitCode 的提示
open System

module PairingCodeInput =
    let normalize (raw0: string) =
        let raw = if isNull raw0 then "" else raw0
        raw
        |> Seq.filter Char.IsDigit
        |> Seq.map (fun ch -> char (int '0' + int (Char.GetNumericValue ch)))
        |> Seq.truncate 6
        |> Seq.toArray
        |> String
    let letterSpacing (digits: string) = (if digits.Length = 0 then 0.0 else 8.0)
    let incomplete = "请输入 6 位配对码。"

type PairingAutoSubmit() =
    let mutable autoSubmitted = ""
    member _.Changed(digits: string) =
        let mutable fire = false
        if digits.Length = 6 then
            if digits <> autoSubmitted then
                autoSubmitted <- digits
                fire <- true
        else
            autoSubmitted <- ""
        fire
