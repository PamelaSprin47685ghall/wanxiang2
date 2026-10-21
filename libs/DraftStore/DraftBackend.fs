namespace Wanxiang.UI

open System
open System.IO
open System.Text
open System.Threading.Tasks

/// 草稿落点（迁自旧 src/Wanxiang.UI/Model/DraftStore.fs 的后端部分）。
/// 桌面是文件（FileDraftBackend）；PWA 是 IndexedDB（宿主侧实现本接口）；测试用内存实现模拟「页面被回收后重开」。
/// 接口签名与旧 IDraftBackend 逐字一致，PWA 侧按同一契约实现 BrowserDraftBackend。
type IDraftBackend =
    abstract Load: unit -> Task<string option>
    abstract Save: string -> Task
    abstract Clear: unit -> Task

/// 桌面：原子写（临时文件 + rename），文件仅当前用户可读写（Q118 同口径）。
type FileDraftBackend(path: string) =
    member _.Path = path

    interface IDraftBackend with
        member _.Load() =
            Task.FromResult(
                try
                    if File.Exists path then
                        Some(File.ReadAllText path)
                    else
                        None
                with _ ->
                    None
            )

        member _.Save(text) =
            try
                Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
                let tmp = path + ".tmp"
                File.WriteAllText(tmp, text, UTF8Encoding false)

                try
                    File.SetUnixFileMode(tmp, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
                with _ ->
                    ()

                File.Move(tmp, path, true)
            with _ ->
                ()

            Task.CompletedTask

        member _.Clear() =
            try
                if File.Exists path then
                    File.Delete path
            with _ ->
                ()

            Task.CompletedTask

    /// 平台默认落点：$WANXIANG_HOME/drafts.json。配置目录由宿主传入
    /// （旧版无参读 LocalState.configHome()；本库不依赖 LocalState，路径拼接结果相同）。
    static member DefaultPath(configHome: string) = Path.Combine(configHome, "drafts.json")

/// 进程内存：测试替身，也是「宿主没有任何持久存储」时的退路（此时草稿仍按进程内存工作，与旧行为一致）。
type MemoryDraftBackend() =
    let mutable stored: string option = None
    member _.Stored = stored

    interface IDraftBackend with
        member _.Load() = Task.FromResult stored

        member _.Save(text) =
            stored <- Some text
            Task.CompletedTask

        member _.Clear() =
            stored <- None
            Task.CompletedTask
