namespace Snapshot

open System
open System.Runtime.InteropServices

/// 改名之后对所在目录 fsync：POSIX 上目录项（文件名）不在文件的页缓存里，
/// 不对目录 fd 执行 fsync，断电后改名本身可能丢失——文件内容完好，却回到旧名字。
/// 只在 Unix 上有这一层；Windows 上 NTFS 元数据由日志保障，改名不需要单独落盘。
/// 失败永远不抛出：断电级别的保证失败只意味着「回旧快照，按规则重建」，不是数据损坏。
module DirFsync =

    [<DllImport("libc", SetLastError = true, EntryPoint = "open")>]
    extern int openPath(string path, int flags)

    [<DllImport("libc", SetLastError = true)>]
    extern int fsync(IntPtr fd)

    [<DllImport("libc", SetLastError = true)>]
    extern int close(IntPtr fd)

    [<Literal>]
    let private O_RDONLY = 0

    [<Literal>]
    let private O_DIRECTORY = 0x100000

    /// 对目录 fsync。仅在 Unix 上有实际动作；任何失败都静默（返回 false 仅供测试观察）。
    let sync (directory: string) : bool =
        if not (OperatingSystem.IsWindows()) then
            let fd = openPath (directory, O_RDONLY ||| O_DIRECTORY)

            if fd < 0 then
                false
            else
                let ok = fsync fd = 0
                close fd |> ignore
                ok
        else
            true
