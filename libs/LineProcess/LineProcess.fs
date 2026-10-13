namespace Wanxiang.Server

open System
open System.Diagnostics
open System.IO
open System.Threading.Tasks

/// 一个按行说话的子进程：标准输入写行、标准输出读行、标准错误转给日志。
/// 环境变量白名单化：只继承 PATH、HOME 等少数几个，再加调用方给的，避免把父进程的密钥带给子进程（Q165）。
type LineProcess(command: string, args: string list, env: Map<string, string>, onLine: string -> unit, onStderr: int -> string -> unit, onExit: string option -> unit) =

    static let inherited = [ "PATH"; "HOME"; "LANG"; "LC_ALL"; "TMPDIR"; "TZ" ]

    let gate = obj ()
    let mutable child: Process = null
    let mutable stdin: StreamWriter = null

    let readLoop (p: Process) =
        task {
            let mutable failure = None
            try
                while not p.StandardOutput.EndOfStream do
                    let line = p.StandardOutput.ReadLine()
                    if not (String.IsNullOrWhiteSpace line) then onLine line
            with e -> failure <- Some e.Message
            onExit failure
        }

    /// 子进程看到的环境变量（白名单 + 调用方给的）。
    static member Environment(env: Map<string, string>) : Map<string, string> =
        let baseEnv =
            [ for key in inherited do
                  match Environment.GetEnvironmentVariable key with
                  | v when not (String.IsNullOrEmpty v) -> yield key, v
                  | _ -> () ]
            |> Map.ofList
        Map.fold (fun acc k v -> Map.add k v acc) baseEnv env

    member _.IsRunning = lock gate (fun () -> not (isNull child) && not child.HasExited)

    /// 启动。已在运行时什么也不做。启动失败抛出异常。
    member _.Start() =
        lock gate (fun () ->
            if isNull child || child.HasExited then
                let psi = ProcessStartInfo command
                for a in args do psi.ArgumentList.Add a
                psi.RedirectStandardInput <- true
                psi.RedirectStandardOutput <- true
                psi.RedirectStandardError <- true
                psi.UseShellExecute <- false
                psi.Environment.Clear()
                for KeyValue(k, v) in LineProcess.Environment env do psi.Environment[k] <- v
                let p = Process.Start psi
                child <- p
                stdin <- p.StandardInput
                p.ErrorDataReceived.Add(fun e -> if not (isNull e.Data) then onStderr p.Id e.Data)
                p.BeginErrorReadLine()
                Task.Run<Task>(Func<Task>(fun () -> readLoop p)) |> ignore)

    /// 写一行并立即刷出。未启动时抛出异常。
    member _.WriteLine(line: string) =
        lock gate (fun () ->
            if isNull stdin then failwith "mcp stdin not ready"
            stdin.WriteLine line
            stdin.Flush())

    /// 连同子进程树一起结束。
    member _.Kill() =
        lock gate (fun () ->
            try (if not (isNull child) && not child.HasExited then child.Kill(entireProcessTree = true)) with _ -> ()
            try (if not (isNull child) then child.Dispose()) with _ -> ()
            child <- null
            stdin <- null)
