namespace Snapshot

open System
open System.IO
open System.Runtime.InteropServices

/// 改名之后对所在目录 fsync：POSIX 上目录项（文件名）不在文件的页缓存里，
/// 不对目录 fd 执行 fsync，断电后改名本身可能丢失——文件内容完好，却回到旧名字。
/// 只在 Unix 上有这一层；Windows 上 NTFS 元数据由日志保障，改名不需要单独落盘。
/// 失败永远不抛出：断电级别的保证失败只意味着「回旧快照，按规则重建」，不是数据损坏。
module DirFsync =

    [<DllImport("libc", SetLastError = true, EntryPoint = "open")>]
    extern int openPath(string path, int flags)

    [<DllImport("libc", SetLastError = true)>]
    extern int fsync(int fd)

    [<DllImport("libc", SetLastError = true)>]
    extern int close(int fd)

    /// POSIX 标准中 O_RDONLY 在所有 Unix/Linux/macOS 平台恒为 0；
    /// 配合 Directory.Exists 先确认目标是目录，避免不同内核架构下 O_DIRECTORY 常数差异。
    [<Literal>]
    let private O_RDONLY = 0

    /// 按平台与架构选择 POSIX O_DIRECTORY，使内核在 open 单一系统调用内拒绝非目录路径，避免先检查再打开的竞争。
    let private oDirectoryFlag () =
        if OperatingSystem.IsMacOS() then
            0x100000
        elif OperatingSystem.IsLinux() then
            match RuntimeInformation.OSArchitecture with
            | Architecture.Arm
            | Architecture.Arm64 -> 0x4000
            | _ -> 0x10000
        else
            0

    /// 对目录 fsync。仅在 Unix 上有实际动作；任何失败都静默（返回 false 仅供测试观察）。
    let sync (directory: string) : bool =
        if String.IsNullOrEmpty directory then
            false
        elif not (OperatingSystem.IsWindows()) then
            let flags = O_RDONLY ||| oDirectoryFlag ()

            if flags = O_RDONLY && not (Directory.Exists directory) then
                false
            else
                let fd = openPath (directory, flags)

                if fd < 0 then
                    false
                else
                    let ok = fsync fd = 0
                    close fd |> ignore
                    ok
        else
            Directory.Exists directory
