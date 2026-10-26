namespace AtomicTextFile

open System
open System.IO
open System.Text
open System.Threading

/// 一个由人和程序共同编辑的文本文件。私有状态：路径、最后一次本对象读到或写出的原文、监视器与去抖计时器。
/// - Write：临时文件 + flush(true) + 原子改名，权限仅当前用户可读写；
/// - Changed：文件被外部改动（含删除、改名）后，去抖 debounce 再发出一次。只说「可能变了」，不读也不判断；
/// - ExternalEdit：磁盘原文与最后已知原文不同时返回磁盘原文（监视器还没来得及通知的外部编辑）。
/// 不理解文本内容。线程安全。
type AtomicTextFile(path: string, ?debounce: TimeSpan) =
    let debounce = defaultArg debounce (TimeSpan.FromMilliseconds 100.0)
    let full = Path.GetFullPath path
    let dir = Path.GetDirectoryName full
    let gate = obj ()
    let changed = Event<unit>()
    let mutable lastKnown: string option = None
    let mutable disposed = false
    let mutable watcher: FileSystemWatcher option = None

    let timer =
        new Timer(
            (fun _ ->
                if not (lock gate (fun () -> disposed)) then
                    changed.Trigger()),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        )

    let utf8 = UTF8Encoding(false)

    let ownerOnly (p: string) =
        if not (OperatingSystem.IsWindows()) then
            File.SetUnixFileMode(p, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)

    let poke () =
        lock gate (fun () ->
            if not disposed then
                try
                    timer.Change(debounce, Timeout.InfiniteTimeSpan) |> ignore
                with :? ObjectDisposedException ->
                    // 并发 Dispose 刚好在锁切换瞬间完成时忽略定时器异常
                    ())

    member _.Path = full

    /// 开始监视外部改动。目录不存在时创建。
    member _.Watch() : unit =
        lock gate (fun () ->
            if watcher.IsNone && not disposed then
                Directory.CreateDirectory dir |> ignore
                let w = new FileSystemWatcher(dir, Path.GetFileName full)
                w.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.FileName ||| NotifyFilters.Size
                w.Changed.Add(fun _ -> poke ())
                w.Created.Add(fun _ -> poke ())
                w.Renamed.Add(fun _ -> poke ())
                w.Deleted.Add(fun _ -> poke ())
                w.EnableRaisingEvents <- true
                watcher <- Some w)

    /// 外部改动（去抖后）。处理器在计时器线程上执行。
    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Exists = File.Exists full

    /// 读原文，并记为最后已知原文。
    member _.Read() : Result<string, string> =
        lock gate (fun () ->
            try
                let text = File.ReadAllText(full, utf8)
                lastKnown <- Some text
                Ok text
            with e ->
                Error(sprintf "read failed: %s" e.Message))

    /// 原子写入，并记为最后已知原文。失败时原文件不变，临时文件被删掉。
    member this.Write(text: string) : Result<unit, string> = this.WriteCore(text, false)

    /// 同 Write，但磁盘原文自最后一次读写以来被别人改过时放弃写入（返回 Error），不覆盖别人的改动。
    /// 临时文件写好并落盘之后、改名之前才检查，把「检查」到「替换」之间的窗口压到最小；
    /// 落盘（fsync）可能很慢，在它之前检查会让别人在落盘期间的写入被悄悄覆盖。
    member this.WriteIfUnchanged(text: string) : Result<unit, string> = this.WriteCore(text, true)

    member private _.WriteCore(text: string, onlyIfUnchanged: bool) : Result<unit, string> =
        lock gate (fun () ->
            Directory.CreateDirectory dir |> ignore

            let tmp =
                Path.Combine(dir, sprintf ".%s.tmp.%d" (Path.GetFileName full) Environment.ProcessId)

            try
                do
                    use fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
                    ownerOnly tmp
                    let bytes = utf8.GetBytes text
                    fs.Write(bytes, 0, bytes.Length)
                    fs.Flush true

                let onDisk =
                    try
                        (if File.Exists full then
                             Some(File.ReadAllText(full, utf8))
                         else
                             None)
                    with _ ->
                        None

                if onlyIfUnchanged && onDisk <> lastKnown then
                    File.Delete tmp
                    failwith "file changed on disk since it was last read; not overwriting"

                File.Move(tmp, full, true)
                lastKnown <- Some text
                Ok()
            with e ->
                (try
                    if File.Exists tmp then
                        File.Delete tmp
                 with _ ->
                     ())

                Error(sprintf "write failed: %s" e.Message))

    /// 磁盘原文与最后已知原文不同（含此前从未读写过）时返回磁盘原文，不更新最后已知原文。
    /// 文件不存在或读不了返回 None。
    member _.ExternalEdit() : string option =
        lock gate (fun () ->
            match
                (try
                    Some(File.ReadAllText(full, utf8))
                 with _ ->
                     None)
            with
            | Some text when Some text <> lastKnown -> Some text
            | _ -> None)

    /// 把文件权限收紧到仅当前用户可读写（外部创建时 umask 可能放宽）。
    member _.RestrictToOwner() : Result<unit, string> =
        try
            ownerOnly full
            Ok()
        with e ->
            Error e.Message

    interface IDisposable with
        member _.Dispose() =
            lock gate (fun () ->
                if not disposed then
                    disposed <- true

                    watcher
                    |> Option.iter (fun w ->
                        w.EnableRaisingEvents <- false
                        w.Dispose())

                    timer.Dispose())
