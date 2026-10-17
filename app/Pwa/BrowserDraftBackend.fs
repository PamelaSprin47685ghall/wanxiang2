namespace Wanxiang.Pwa

open System
open System.Threading.Tasks
open Wanxiang.Interop
open Wanxiang.UI

/// PWA：IndexedDB 草稿后端（经 BrowserDrafts；wwwroot/main.js 的 draftGet/draftPut/draftDelete）。
/// IDraftBackend 接口由 wanxiang2 libs 层定义，签名与根 src/Wanxiang.UI/Model/DraftStore.fs
/// 逐字一致；本文件只提供浏览器实现，宿主接线（写 / 删）见 Program.fs 的 applyDraftWrite。
type BrowserDraftBackend() =
    interface IDraftBackend with
        member _.Load() =
            task {
                try
                    let! text = BrowserDrafts.Get()
                    return if String.IsNullOrWhiteSpace text then None else Some text
                with _ -> return None
            }
        member _.Save(text) =
            task { try do! BrowserDrafts.Put text with _ -> () } :> Task
        member _.Clear() =
            task { try do! BrowserDrafts.Delete() with _ -> () } :> Task
