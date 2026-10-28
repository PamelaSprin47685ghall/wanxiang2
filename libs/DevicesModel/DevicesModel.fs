namespace Wanxiang.UI

open System
open System.Net.NetworkInformation
open System.Net.Sockets
open QRCoder

/// 手机应该打开哪个地址。
[<RequireQualifiedAccess>]
type InviteTarget =
    /// 主机地址对外可达：直接用它。
    | Reachable of url: string
    /// 主机只监听本机回环地址：手机无法直接访问；候选是这台机器的局域网地址（可能为空）。
    | LoopbackOnly of candidates: string list
    /// 还没有连接地址。
    | NoAddress

module DevicesModel =

    /// ws(s)://host:port/ws → http(s)://host:port/（同源 PWA 的入口）。
    let httpOrigin (wsUrl: string) : string option =
        match Uri.TryCreate((if isNull wsUrl then "" else wsUrl.Trim()), UriKind.Absolute) with
        | true, uri when uri.Scheme = "ws" || uri.Scheme = "wss" ->
            let scheme = if uri.Scheme = "wss" then "https" else "http"
            Some(UriBuilder(scheme, uri.Host, uri.Port, "/").Uri.ToString())
        | _ -> None

    let isLoopbackHost (wsUrl: string) : bool =
        match Uri.TryCreate((if isNull wsUrl then "" else wsUrl.Trim()), UriKind.Absolute) with
        | true, uri -> uri.IsLoopback || uri.Host = "localhost"
        | _ -> false

    /// 本机的局域网 IPv4 地址（桌面端；浏览器或受限环境返回空）。
    let lanAddresses () : string list =
        try
            [
                for nic in NetworkInterface.GetAllNetworkInterfaces() do
                    if
                        nic.OperationalStatus = OperationalStatus.Up
                        && nic.NetworkInterfaceType <> NetworkInterfaceType.Loopback
                    then
                        for ua in nic.GetIPProperties().UnicastAddresses do
                            if ua.Address.AddressFamily = AddressFamily.InterNetwork then
                                yield ua.Address.ToString()
            ]
            |> List.distinct
        with _ ->
            []

    /// 手机该打开的地址。`lan` 注入便于测试。
    let inviteTarget (wsUrl: string) (lan: string list) : InviteTarget =
        match httpOrigin wsUrl with
        | None -> InviteTarget.NoAddress
        | Some origin ->
            if isLoopbackHost wsUrl then
                let port = (Uri origin).Port
                let scheme = (Uri origin).Scheme
                InviteTarget.LoopbackOnly [ for ip in lan -> UriBuilder(scheme, ip, port, "/").Uri.ToString() ]
            else
                InviteTarget.Reachable origin

    /// 二维码模块矩阵（true = 深色模块）。使用成熟库 QRCoder 编码，不自行实现（规范 58）。
    let qrMatrix (text: string) : bool[][] =
        use generator = new QRCodeGenerator()
        use data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M)
        // QRCoder 的 ModuleMatrix 自带 4 模块静区；这里去掉，由绘制层统一补静区（QrCodeView）。
        let quiet = 4
        let size = data.ModuleMatrix.Count - quiet * 2

        [|
            for r in 0 .. size - 1 -> [| for c in 0 .. size - 1 -> data.ModuleMatrix[r + quiet][c + quiet] |]
        |]
