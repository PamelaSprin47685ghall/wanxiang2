namespace Wanxiang.Server

open System
open System.IO
open System.Net.Sockets

/// 对本机的只读探测：端口是否有人监听、磁盘剩余空间、目录是否对组或其他用户开放。不创建、不修改任何东西。
module HostProbe =

    /// "host:port" 上 300 毫秒内能连上就算占用；格式不对或连不上算未占用。
    let portInUse (listen: string) : bool =
        try
            let parts = listen.Split ':'

            if parts.Length <> 2 then
                false
            else
                use client = new TcpClient()
                let ar = client.BeginConnect(parts[0], int parts[1], null, null)
                ar.AsyncWaitHandle.WaitOne 300 |> ignore
                client.EndConnect ar |> ignore
                true
        with _ ->
            false

    /// 路径所在磁盘的可用字节数；磁盘未就绪时为 Int64.MaxValue；拿不到信息时为 None。
    let freeBytes (path: string) : int64 option =
        try
            let drive = DriveInfo(Path.GetPathRoot path)

            Some(
                if drive.IsReady then
                    drive.AvailableFreeSpace
                else
                    Int64.MaxValue
            )
        with _ ->
            None

    /// 存在且组或其他用户有任何权限的目录。读不了权限的目录不算。
    let groupOrOtherAccessible (dirs: string list) : string list =
        let open' =
            int (
                UnixFileMode.GroupRead
                ||| UnixFileMode.GroupWrite
                ||| UnixFileMode.GroupExecute
                ||| UnixFileMode.OtherRead
                ||| UnixFileMode.OtherWrite
                ||| UnixFileMode.OtherExecute
            )

        dirs
        |> List.filter (fun dir ->
            Directory.Exists dir
            && (try
                    (int (File.GetUnixFileMode dir) &&& open') <> 0
                with _ ->
                    false))
