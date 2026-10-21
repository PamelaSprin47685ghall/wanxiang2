namespace Wanxiang.UI

open System
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks

/// 决策 251：单实例转发。主实例在配置目录下监听一个本机 Unix 套接字（Windows 10+ 同样支持），
/// 第二次启动（系统打开链接时就是这样）先连它：连上就把链接（或空行 = 只是唤起）交过去后退出，
/// 不再去抢数据锁；连不上才当主实例。
module SingleInstance =

    let socketPath (configDir: string) = Path.Combine(configDir, "ui.sock")

    /// 尝试交给已在运行的实例。成功返回 true（调用方应退出）。
    let tryForward (path: string) (url: string option) : bool =
        if not (File.Exists path) then
            false
        else
            try
                use s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                s.Connect(UnixDomainSocketEndPoint path)

                let line =
                    (url |> Option.defaultValue "").Replace("\n", "").Replace("\r", "") + "\n"

                s.Send(Encoding.UTF8.GetBytes line) |> ignore
                let buf = Array.zeroCreate<byte> 3
                s.ReceiveTimeout <- 2000

                let n =
                    try
                        s.Receive buf
                    with _ ->
                        0

                n > 0 && Encoding.ASCII.GetString(buf, 0, n).StartsWith "ok"
            with _ ->
                false

    /// 主实例监听。每条连接读一行：非空就当链接交给 onLink，空行只唤起窗口（onActivate）。
    let listen (path: string) (onLink: string -> unit) (onActivate: unit -> unit) : IDisposable =
        try
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore

            if File.Exists path then
                (try
                    File.Delete path
                 with _ ->
                     ())

            let server =
                new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)

            server.Bind(UnixDomainSocketEndPoint path)

            try
                File.SetUnixFileMode(path, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
            with _ ->
                ()

            server.Listen 4
            let cts = new CancellationTokenSource()

            let loop () =
                task {
                    while not cts.IsCancellationRequested do
                        try
                            let! client = server.AcceptAsync(cts.Token)
                            use client = client
                            use stream = new NetworkStream(client)
                            use reader = new StreamReader(stream, Encoding.UTF8)
                            let readTask = reader.ReadLineAsync()
                            let! finished = Task.WhenAny(readTask, Task.Delay 2000)

                            if obj.ReferenceEquals(finished, readTask) then
                                let line = readTask.Result
                                let ack = Encoding.ASCII.GetBytes "ok\n"
                                stream.Write(ack, 0, ack.Length)

                                match line with
                                | null -> ()
                                | l when String.IsNullOrWhiteSpace l -> onActivate ()
                                | l -> onLink (l.Trim())
                        with _ ->
                            ()
                }

            Task.Run(fun () -> loop () :> Task) |> ignore

            { new IDisposable with
                member _.Dispose() =
                    cts.Cancel()

                    (try
                        server.Dispose()
                     with _ ->
                         ())

                    (try
                        File.Delete path
                     with _ ->
                         ())
            }
        with _ ->
            { new IDisposable with
                member _.Dispose() = ()
            }
