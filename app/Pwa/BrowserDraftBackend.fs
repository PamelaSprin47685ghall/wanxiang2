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
                with _ ->
                    return None
            }

        member _.Save(text) =
            task {
                try
                    do! BrowserDrafts.Put text
                with ex ->
                    // 浏览器隐私模式或配额满导致 IndexedDB 写入失败时只记诊断，不打断输入
                    eprintfn "[wanxiang] 保存浏览器草稿失败：%s" ex.Message
            }
            :> Task

        member _.Clear() =
            task {
                try
                    do! BrowserDrafts.Delete()
                with ex ->
                    // 清理浏览器草稿失败只记诊断，不阻断后续流程
                    eprintfn "[wanxiang] 清理浏览器草稿失败：%s" ex.Message
            }
            :> Task
