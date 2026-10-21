namespace Wanxiang.App

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Input.Platform
open Avalonia.Media.Imaging
open Avalonia.Platform.Storage
open Avalonia.Threading
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

/// wanxiang2 独立桌面入口（PLAN.md GAP1 的一半）。
/// 非界面逻辑全部经 Wanxiang2.* 0.0.0-local 交付包消费（命名空间沿用 Wanxiang.App / Wanxiang.Server），
/// 与宿主 src/Wanxiang.App/Program.fs 逐段对应；UI 壳由批次 2 新建的 AppShell（ShellComposer）与
/// EntryShells（App / MainWindow）提供，本入口只实现 ShellPorts 端口与 App 静态端口注入。
module Program =

    /// info 日志统一走 Wanxiang.Server.Stderr（结构化 JSON Lines + 已知密钥脱敏，决策 41/198）。
    /// 该模块已随 wanxiang2/compose/HostLog 迁入，命名空间沿用 Wanxiang.Server。
    let logInfo (msg: string) = Wanxiang.Server.Stderr.info msg

    [<EntryPoint; STAThread>]
    let main argv =
        try
            let parsed = Cli.parse argv

            let cli =
                if parsed.help then
                    parsed
                else
                    { parsed with
                        config = parsed.config |> Option.map Wanxiang.UI.LocalState.requireOutsideGit
                        data = parsed.data |> Option.map Wanxiang.UI.LocalState.requireOutsideGit
                        bundle = parsed.bundle |> Option.map Wanxiang.UI.LocalState.requireOutsideGit
                    }

            if cli.help then
                eprintfn "%s" Cli.usage
                exit 0
            // 路径与开关（决策 60：命令行 > TOML > 程序默认）由 LaunchPlan 推导
            let home = Wanxiang.UI.LocalState.configHome ()
            let requestedConfig, requestedData = Cli.paths cli home
            let configPath = Wanxiang.UI.LocalState.requireOutsideGit requestedConfig
            let dataDir = Wanxiang.UI.LocalState.requireOutsideGit requestedData
            // LaunchPlan 会读 TOML；派生的默认文件也必须先通过路径检查。
            let plan =
                LaunchPlan.ofOptions
                    { cli with
                        config = Some configPath
                        data = Some dataDir
                    }
                    home

            let switches = plan.switches
            let isDoctorOnly = RunModes.isDoctorOnly switches

            if not isDoctorOnly || switches.fix then
                let workspace = Directory.GetCurrentDirectory()

                if home = Wanxiang.UI.LocalState.rootFor workspace then
                    Wanxiang.UI.LocalState.initialize workspace |> ignore
            // 偏好与客户端令牌跟随本次配置，不暗读旧的用户级目录。
            Environment.SetEnvironmentVariable("WANXIANG_HOME", Path.GetDirectoryName configPath)

            // 决策 251：桌面已在运行时（系统打开链接 = 再启动一次），把链接交给它后退出，
            // 不去抢数据锁、也不弹第二个窗口。
            let instanceSocket =
                Wanxiang.UI.SingleInstance.socketPath (Path.GetDirectoryName configPath)

            if
                switches.client
                && Wanxiang.UI.SingleInstance.tryForward instanceSocket cli.openUrl
            then
                logInfo "forwarded to the running wanxiang window"
                exit 0

            cli.openUrl |> Option.iter Wanxiang.UI.ExternalLinks.post

            // 决策 50：pwa 开关 = 托管 PWA 静态资源。PWA 由 S 托管并依赖其 /ws 端点（决策 2），
            // 因此 pwa=true 隐含 server 功能——pwa-only 组合 = 托管 PWA + WebSocket 端点的无 UI 模式，
            // 绝不静默空转挂起。
            let effectiveServer = RunModes.runsHost switches

            if isDoctorOnly then
                // 0000：只读 doctor；0001：doctor + 修复（决策 51）
                if switches.fix then
                    logInfo "doctor --fix: repairing log"
                    // 决策 105/Q110：任何可能修改日志的修复必须在持锁后进行（StoreLaunch 负责）
                    match StoreLaunch.fix dataDir logInfo with
                    | Ok() -> ()
                    | Error e ->
                        eprintfn "%s" e
                        exit 1
                // O-002：`doctor --bundle <path>` 生成 support bundle（默认只收集元数据，
                // 禁止 apiKey/token/完整用户消息/完整附件）。
                // 先建 bundle（配置损坏/日志损坏正是最需要诊断的时候——不能因为诊断失败而不产出证据），
                // 再跑只读 doctor 并打印。
                match cli.bundle with
                | Some bundlePath ->
                    match Wanxiang.Server.SupportBundle.create dataDir configPath bundlePath with
                    | Ok dir -> logInfo (sprintf "support bundle written to %s" dir)
                    | Error e ->
                        eprintfn "support bundle failed: %s" e
                        exit 1
                | None -> ()

                let d = Wanxiang.Server.Doctor.run (configPath, dataDir)
                Wanxiang.Server.Doctor.print d
                exit 0

            // 启动前先执行 doctor/fix（决策 51 第二问：先检查修复，再启动组件）。
            // Q107：纯客户端（无 S）不检查本地数据目录状态，不被数据问题阻塞启动。
            // 数据锁（决策 105/Q110）：任何可能修改 NDJSON 的修复必须在持锁后进行。
            let replayStartup () =
                if not effectiveServer then
                    None
                else
                    // S-001 / S-003：只读检查，损坏时只读降级启动；格式不支持、读不了才拒绝启动（StoreLaunch 负责）。
                    // 打开的存储直接交给 ServerApp（不再重放第二次）。
                    match StoreLaunch.openForStartup dataDir logInfo with
                    | Ok store -> Some store
                    | Error e ->
                        eprintfn "%s" e
                        exit 1

            // 自动配对（决策 64 + Q190）：server+client 同进程时，本机桌面 Client 自动获得永久令牌。
            // - 主机 TOML 只存哈希；客户端令牌原文保存到本机 client.toml（决策 54）；
            // - 任一侧丢失（哈希不在主机 / 客户端令牌文件丢失）时创建新 token；
            // - 写 TOML 走 ConfigStore.Rewrite 原子路径（临时文件 + rename + reload，决策 42/43）。
            if RunModes.autoPairsDesktop switches then
                match DesktopPairing.ensure configPath with
                | Ok() -> ()
                | Error e -> logInfo (sprintf "desktop auto-pairing skipped: %s" e)

            // 主机（若启用）：后台线程运行。
            let startServer () =
                let startupReplayOutcome = replayStartup ()

                if effectiveServer then
                    let pwaDir =
                        if switches.pwa then
                            PwaAssets.locate AppContext.BaseDirectory
                        else
                            None

                    let app =
                        new Wanxiang.Server.ServerApp(
                            dataDir,
                            configPath,
                            false,
                            pwaDir,
                            logInfo,
                            ?startupStore = startupReplayOutcome,
                            // 产品版本取本程序集（Wanxiang.App）版本；旧实现取 Wanxiang.Server 程序集版本，产品构建时两者相同
                            ?hostVersion =
                                (match System.Reflection.Assembly.GetExecutingAssembly().GetName().Version with
                                 | null -> None
                                 | v -> Some(v.ToString()))
                        )

                    app.Start(switches.pwa)
                    Some app
                else
                    None

            // 决策 291：主机 + 桌面同进程时，事件日志重放与主机启动放到后台，窗口先画出来。
            // 历史越长重放越久，用户却只需要先看到界面骨架；客户端的首次连接会等这个门
            // （LocalHostGate），不会因为主机还没监听而先报一次「连接失败」再退避重连。
            // 纯主机模式没有界面可先画，仍同步启动。
            let serverTask: Task<Wanxiang.Server.ServerApp option> =
                if RunModes.hostInBackground switches then
                    Task.Run(fun () -> startServer ())
                else
                    Task.FromResult(startServer ())
            // 决策 291：主机启动门接线（libs/StartupTrace/LocalHostGate）——
            // 客户端首次连接前等主机重放与监听完成，不先报「连接失败」再退避重连。
            Wanxiang.UI.LocalHostGate.ready <- (serverTask :> Task)

            let serverApp () =
                try
                    if serverTask.Wait(TimeSpan.FromSeconds 15.0) then
                        serverTask.Result
                    else
                        None
                with _ ->
                    None

            let stopServer () =
                match serverApp () with
                | Some app -> app.Stop()
                | None -> ()

                logInfo "wanxiang stopped"

            if switches.client then
                // 桌面客户端（决策 49：同进程 UI，通过真实 WebSocket 连接本机 S）
                // 有本地 S 时自动连接 loopback（决策 62）；无 S 时由用户在 UI 中选择（决策 62）
                // Q101/102：默认 UI+S 模式同样注册信号处理——第一次信号优雅排空（连接关闭、
                // 单写者 flush）后退出，第二次立即退出（强制终止由外部管理器负责）
                let signals = ShutdownSignals()

                let shutdownUi () =
                    match signals.Signal() with
                    | ShutdownStep.Force -> Environment.Exit ShutdownSignals.ForceExitCode
                    | ShutdownStep.Graceful ->
                        logInfo "shutting down gracefully (signal; send again to force)"
                        stopServer ()
                        Environment.Exit 0

                let ctrlHandler =
                    ConsoleCancelEventHandler(fun _ e ->
                        e.Cancel <- true
                        shutdownUi ())

                Console.CancelKeyPress.AddHandler ctrlHandler

                let sigterm =
                    try
                        Some(
                            PosixSignalRegistration.Create(
                                PosixSignal.SIGTERM,
                                Action<PosixSignalContext>(fun ctx ->
                                    ctx.Cancel <- true
                                    shutdownUi ())
                            )
                        )
                    with _ ->
                        None
                // 桌面 UI 入口（决策 48：本机客户端也必须走真实 WebSocket，入口仅负责 AppBuilder 平台启动）
                Wanxiang.UI.UrlSchemeRegistration.registerCurrentUser logInfo

                let mainWindow () =
                    match Application.Current with
                    | null -> None
                    | app ->
                        match app.ApplicationLifetime with
                        | :? IClassicDesktopStyleApplicationLifetime as l when not (isNull l.MainWindow) ->
                            Some l.MainWindow
                        | _ -> None

                let activate () =
                    Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                        mainWindow ()
                        |> Option.iter (fun w ->
                            if w.WindowState = Avalonia.Controls.WindowState.Minimized then
                                w.WindowState <- Avalonia.Controls.WindowState.Normal

                            w.Activate()))

                use _instance =
                    Wanxiang.UI.SingleInstance.listen
                        instanceSocket
                        (fun url ->
                            Wanxiang.UI.ExternalLinks.post url
                            activate ())
                        activate
                // 决策 251：Avalonia 主线程初始化后，macOS 打开链接走 OpenUri 激活。
                Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                    try
                        match Application.Current with
                        | null -> ()
                        | app ->
                            match app.TryGetFeature<IActivatableLifetime>() with
                            | null -> ()
                            | lifetime ->
                                lifetime.Activated.Add(fun e ->
                                    match e with
                                    | :? ProtocolActivatedEventArgs as p when
                                        p.Kind = ActivationKind.OpenUri && not (isNull p.Uri)
                                        ->
                                        Wanxiang.UI.ExternalLinks.post (p.Uri.ToString())
                                    | _ -> ())
                    with _ ->
                        ())

                let runDesktopUi (argv: string array) : int =
                    // 代码着色与公式排版跑在进程内的 JS 引擎上。基座脚本解析要几百毫秒，
                    // 放到后台线程先付掉；没预热完也只是第一次渲染慢，不影响正确性。
                    let rich, backend = RichJintAdapter.create ()
                    Wanxiang.UI.RichBackend.install backend
                    Task.Run(fun () -> rich.Warmup()) |> ignore

                    // ---- 批次 2 接线：ShellComposer + EntryShells（参照 PWA 入口的完整接线）----
                    // 真实 WebSocket 客户端（WsClient 经 AppShell 包传递引用；命名空间 Wanxiang.Client）。
                    let client = Wanxiang.Client.WsClient()
                    let mutable shell: ShellComposer = Unchecked.defaultof<_>
                    let mutable overlay: Wanxiang2.OverlayDesk.OverlayDesk = Unchecked.defaultof<_>

                    let mutable composerDesk: Wanxiang2.ComposerDesk.ComposerDesk =
                        Unchecked.defaultof<_>

                    let mutable rootGrid: Grid = null
                    /// 视图只组装一次：Build 与 Root 都调它，重复组装会换掉窗口里那棵树。
                    let mutable builtViews: ShellViews option = None
                    /// 代码折行偏好（唯一实例）：MessageCards 与 SetCodeWrap 端口共用同一个。
                    let codeWrap = Wanxiang2.MarkdownView.CodeWrapMemory(false)
                    /// 顶层按键入口（决策 454）只挂一次：记下已挂的 TopLevel。
                    let mutable topLevelKeysHooked: TopLevel option = None

                    /// 后台回调一律投递回 UI 线程（与旧壳的 ui.Post 同一约定）。
                    let post (action: unit -> unit) =
                        Dispatcher.UIThread.Post(fun () -> action ())

                    /// 本窗口的 TopLevel：对话框、剪贴板、启动器都要经它（Window 是 TopLevel 子类）。
                    let topLevel () : TopLevel =
                        let fromWindow =
                            match Avalonia.Application.Current with
                            | null -> None
                            | app ->
                                match app.ApplicationLifetime with
                                | :? IClassicDesktopStyleApplicationLifetime as lifetime when
                                    not (isNull lifetime.MainWindow)
                                    ->
                                    Some(lifetime.MainWindow :> TopLevel)
                                | _ -> None

                        match fromWindow with
                        | Some top -> top
                        | None ->
                            if isNull (box rootGrid) then
                                null
                            else
                                TopLevel.GetTopLevel rootGrid

                    let mainWindow () =
                        match topLevel () with
                        | :? Window as window -> Some window
                        | _ -> None

                    // 计时器归入口壳：DispatcherTimer 包装成 ShellPorts.After / Every。
                    let after (delay: TimeSpan) (action: unit -> unit) : IDisposable =
                        let timer = DispatcherTimer()
                        timer.Interval <- delay

                        timer.Tick.Add(fun _ ->
                            timer.Stop()
                            action ())

                        timer.Start()

                        { new IDisposable with
                            member _.Dispose() = timer.Stop()
                        }

                    let every (interval: TimeSpan) (action: unit -> unit) : IDisposable =
                        let timer = DispatcherTimer()
                        timer.Interval <- interval
                        timer.Tick.Add(fun _ -> action ())
                        timer.Start()

                        { new IDisposable with
                            member _.Dispose() = timer.Stop()
                        }

                    // ---- 提示条（OverlayDesk；浮层在 buildViews 里构造）----
                    let toast (text: string) (tone: ShellToastTone) =
                        match tone with
                        | ShellToastTone.Info -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Info)
                        | ShellToastTone.Success -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Success)
                        | ShellToastTone.Warning -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Warning)
                        | ShellToastTone.Failure -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Failure)

                    let toastUndo (text: string) (undo: unit -> unit) =
                        overlay.ToastWith(text, Wanxiang2.ToastStrip.ToastTone.Info, Some("撤销", undo))

                    // ---- 桌面 IO 端口（ShellPorts 的 IO 面；浏览器端口给空实现）----
                    // 剪贴板写：Avalonia 的剪贴板在 TopLevel.Clipboard（Application 没有 Clipboard）。
                    // 写入可能因权限、焦点被拒：等写入落地再报，成功与失败各自说话（与旧 copyToClipboard 同文案）。
                    let copyToClipboard (text: string) =
                        try
                            match topLevel () with
                            | null -> toast "无法访问剪贴板，复制失败。" ShellToastTone.Failure
                            | top ->
                                match top.Clipboard with
                                | null -> toast "无法访问剪贴板，复制失败。" ShellToastTone.Failure
                                | clip ->
                                    async {
                                        try
                                            do! clip.SetTextAsync text |> Async.AwaitTask
                                            post (fun () -> toast "已复制到剪贴板" ShellToastTone.Success)
                                        with _ ->
                                            post (fun () -> toast "复制到剪贴板失败，请重试。" ShellToastTone.Failure)
                                    }
                                    |> Async.Start
                        with _ ->
                            ()
                    // 系统浏览器打开链接（wanxiang:// 与消息链接由组合者先行分流，见 ShellComposer.OpenUrl）
                    let openLink (url: string) =
                        try
                            match topLevel () with
                            | null -> ()
                            | top -> top.Launcher.LaunchUriAsync(Uri url) |> ignore
                        with _ ->
                            toast (sprintf "无法打开链接：%s" url) ShellToastTone.Warning
                    // 下载落点：保存窗口 + 写文件；取消不报，失败说明原因。
                    let saveDownload (fileName: string) (bytes: byte[]) =
                        match topLevel () with
                        | null -> ()
                        | top ->
                            async {
                                try
                                    let! file =
                                        top.StorageProvider.SaveFilePickerAsync(
                                            FilePickerSaveOptions(SuggestedFileName = fileName)
                                        )
                                        |> Async.AwaitTask

                                    if not (isNull file) then
                                        use! stream = file.OpenWriteAsync() |> Async.AwaitTask
                                        do! stream.WriteAsync(bytes, 0, bytes.Length) |> Async.AwaitTask
                                        post (fun () -> toast (UploadFacts.savedText fileName) ShellToastTone.Success)
                                with ex ->
                                    post (fun () ->
                                        toast (UploadFacts.saveFailedText ex.Message) ShellToastTone.Failure)
                            }
                            |> Async.Start
                    // 系统深色偏好：桌面来自系统设置（不能用 RequestedThemeVariant——它被固定为 Light）。
                    let systemPrefersDark () : bool =
                        try
                            match Avalonia.Application.Current with
                            | null -> false
                            | app ->
                                match app.PlatformSettings with
                                | null -> false
                                | settings ->
                                    settings.GetColorValues().ThemeVariant = Avalonia.Platform.PlatformThemeVariant.Dark
                        with _ ->
                            false
                    // 图片缩略图：入口解码成 IImage 交给输入区（解码失败静默，与旧 Composer.SetThumbnail 一致）。
                    let makeThumbnail (attachmentId: Guid) (bytes: byte[]) =
                        try
                            use stream = new MemoryStream(bytes)
                            let image = Bitmap.DecodeToWidth(stream, 96)

                            if not (isNull image) then
                                composerDesk.ApplyThumbnail(attachmentId, image :> Avalonia.Media.IImage)
                        with _ ->
                            ()
                    // 草稿落点接线（宿主桥）：桌面写 $WANXIANG_HOME/drafts.json
                    //（原子写 tmp+rename、0600、Q118 同口径；WANXIANG_HOME 已在上方设为 configPath 的目录）。
                    let draftBackend: IDraftBackend =
                        FileDraftBackend(FileDraftBackend.DefaultPath(Path.GetDirectoryName configPath))

                    let applyDraftWrite (write: DraftWrite) =
                        match write with
                        | DraftWrite.Write text -> draftBackend.Save text
                        | DraftWrite.Delete -> draftBackend.Clear()
                        | DraftWrite.Unchanged -> Task.CompletedTask
                        |> ignore
                    // 浏览器端口：桌面不用，空实现（与 AppShell「浏览器端口 PWA 才用」注释一致）
                    let browserViewportWidth () : float = 0.0
                    let browserResumeEpoch () : int = 0
                    let browserPageUrl () : string = ""
                    let browserConversationSucceeded () = ()
                    let browserAddHidden (_: unit -> unit) = ()

                    // ---- 附件：选文件 / 拖入 / 粘贴（读字节归入口；归属判定与分块归组合者）----
                    // 读完一个文件立刻交一个：读失败不牵连同批已上传的（契约 v1.1 §1）。
                    let readStorageFile (file: IStorageFile) : Async<byte[]> =
                        async {
                            use! stream = file.OpenReadAsync() |> Async.AwaitTask
                            use buffer = new MemoryStream()
                            do! stream.CopyToAsync buffer |> Async.AwaitTask
                            return buffer.ToArray()
                        }

                    let pickAttachment () =
                        match topLevel () with
                        | null -> ()
                        | top ->
                            // 归属在打开选择窗口前取：用户在选择窗口里的期间换主机/重连，读完就不上传。
                            let owner = shell.UploadOwner()

                            async {
                                try
                                    let! picked =
                                        top.StorageProvider.OpenFilePickerAsync(
                                            FilePickerOpenOptions(
                                                AllowMultiple = true,
                                                Title = "添加附件",
                                                FileTypeFilter =
                                                    [
                                                        for (label, patterns, mimes) in MediaTypes.pickerFilters ->
                                                            FilePickerFileType(
                                                                label,
                                                                Patterns = patterns,
                                                                MimeTypes = mimes
                                                            )
                                                    ]
                                            )
                                        )
                                        |> Async.AwaitTask

                                    for file in picked do
                                        let! bytes = readStorageFile file
                                        let name = file.Name
                                        post (fun () -> shell.AttachFiles owner [ (name, bytes) ])

                                    post (fun () -> composerDesk.Focus())
                                with ex ->
                                    post (fun () ->
                                        toast (UploadFacts.readFailedText ex.Message) ShellToastTone.Failure
                                        composerDesk.Focus())
                            }
                            |> Async.Start

                    let dropFiles (items: IStorageItem list) =
                        match topLevel () with
                        | null -> ()
                        | _ ->
                            let owner = shell.UploadOwner()

                            async {
                                try
                                    for item in items do
                                        match item with
                                        | :? IStorageFile as sf ->
                                            let! bytes = readStorageFile sf
                                            let name = sf.Name
                                            post (fun () -> shell.AttachFiles owner [ (name, bytes) ])
                                        | _ ->
                                            let path = item.TryGetLocalPath()

                                            if not (String.IsNullOrEmpty path) && File.Exists path then
                                                let bytes = File.ReadAllBytes path
                                                let name = Path.GetFileName path
                                                post (fun () -> shell.AttachFiles owner [ (name, bytes) ])

                                    post (fun () -> composerDesk.Focus())
                                with ex ->
                                    post (fun () ->
                                        toast (UploadFacts.dropReadFailedText ex.Message) ShellToastTone.Failure
                                        composerDesk.Focus())
                            }
                            |> Async.Start
                    // 剪贴板探查同步决定是否接管（格式枚举最多等 100 ms），内容异步交付；
                    // 都不命中就返回 false，让输入框走默认粘贴。
                    let pasteFromClipboard () : bool =
                        match topLevel () with
                        | null -> false
                        | top ->
                            try
                                let clipboard = top.Clipboard

                                if isNull clipboard then
                                    false
                                else
                                    let formatsTask = clipboard.GetDataFormatsAsync()

                                    if formatsTask.Wait(100) && formatsTask.Result <> null then
                                        let formats = formatsTask.Result

                                        let hasFiles =
                                            formats |> Seq.exists (fun f -> f = Avalonia.Input.DataFormat.File)

                                        let hasBitmap =
                                            formats |> Seq.exists (fun f -> f = Avalonia.Input.DataFormat.Bitmap)

                                        match UploadFacts.pasteSource hasFiles hasBitmap with
                                        | UploadFacts.PasteSource.Files ->
                                            async {
                                                try
                                                    let! files = clipboard.TryGetFilesAsync() |> Async.AwaitTask

                                                    if files <> null && files.Length > 0 then
                                                        post (fun () -> dropFiles (List.ofArray files))
                                                with ex ->
                                                    post (fun () ->
                                                        toast
                                                            (UploadFacts.clipboardFilesFailedText ex.Message)
                                                            ShellToastTone.Failure

                                                        composerDesk.Focus())
                                            }
                                            |> Async.Start

                                            true
                                        | UploadFacts.PasteSource.Bitmap ->
                                            let owner = shell.UploadOwner()

                                            async {
                                                try
                                                    let! bitmap = clipboard.TryGetBitmapAsync() |> Async.AwaitTask

                                                    if bitmap <> null then
                                                        use buffer = new MemoryStream()
                                                        let options = PngBitmapEncoderOptions()
                                                        bitmap.Save(buffer, options)
                                                        let bytes = buffer.ToArray()
                                                        let name = UploadFacts.pasteFileName DateTime.Now

                                                        post (fun () ->
                                                            shell.AttachFiles owner [ (name, bytes) ]
                                                            composerDesk.Focus())
                                                with ex ->
                                                    post (fun () ->
                                                        toast
                                                            (UploadFacts.clipboardImageFailedText ex.Message)
                                                            ShellToastTone.Failure

                                                        composerDesk.Focus())
                                            }
                                            |> Async.Start

                                            true
                                        | UploadFacts.PasteSource.NotHandled -> false
                                    else
                                        false
                            with _ ->
                                false

                    // 连接 / 认证发送（真实 WsClient；须在 buildViews 之前定义，ApprovalDesk 构造引用 send）
                    // 目标 URL 由组合者给出（旧 lastUrl 归组合者）。
                    let connectAsync (url: string) : Async<int> =
                        async {
                            // 决策 291：同进程主机还在重放时先等它。门失败只说明本机主机没起来，
                            // 不代替这次连接的结果（用户可能连的是另一台主机）。
                            do!
                                Wanxiang.UI.LocalHostGate.ready.ContinueWith(fun (_: Task) -> ())
                                |> Async.AwaitTask
                            // 失败必须抛给组合者。吞成 -1 会让界面停在「正在连接」，
                            // 既不报原因也不再退避（PWA 入口不吞）。
                            let! epoch =
                                client.ConnectWithGenerationAsync(Uri url, CancellationToken.None)
                                |> Async.AwaitTask

                            return epoch
                        }

                    let sendAuth (epoch: int) (ev: WireEvent) =
                        client.TrySendAtGenerationAsync(epoch, ev) |> ignore

                    let send (ev: WireEvent) = client.TrySendAsync ev |> ignore
                    let sendCommand (cmd: ClientCommand) : Task<bool> = client.TrySendCommandAsync cmd
                    // 导出分页读取：按当前连接代次发送（false = 没发出去）。
                    let sendAtGeneration (ev: WireEvent) : Task<bool> =
                        client.TrySendAtGenerationAsync(client.ConnectionGeneration, ev)
                    // Ctrl+Z 让位给文本撤销：焦点落在有文字的输入框里才算「正在编辑」。
                    let isEditingText () : bool =
                        match topLevel () with
                        | null -> false
                        | top ->
                            match top.FocusManager with
                            | null -> false
                            | manager ->
                                match manager.GetFocusedElement() |> Option.ofObj with
                                | Some(:? TextBox as box) -> not (String.IsNullOrEmpty box.Text)
                                | _ -> false

                    // ---- 连接事实与事件订阅（代次过滤 + UI 线程 marshal 归入口）----
                    let connectionGeneration () : int = client.ConnectionGeneration
                    let isConnected () : bool = client.IsConnected
                    // 焦点归宿：记下触发控件，关闭设置时还给它（控件已不可用则交回组合者兜底）。
                    let captureFocus () : unit -> bool =
                        let focused =
                            match topLevel () with
                            | null -> None
                            | top ->
                                match top.FocusManager with
                                | null -> None
                                | manager ->
                                    match manager.GetFocusedElement() with
                                    | :? Control as control -> Some control
                                    | _ -> None

                        match focused with
                        | Some control ->
                            fun () ->
                                try
                                    if
                                        control.IsEnabled
                                        && control.IsEffectivelyVisible
                                        && not (isNull (TopLevel.GetTopLevel control))
                                    then
                                        control.Focus() |> ignore
                                        true
                                    else
                                        false
                                with _ ->
                                    false
                        | None -> fun () -> false

                    let subscribeHostEvents (handler: WireEvent -> unit) : IDisposable =
                        client.ConnectionEventReceived.Subscribe(fun (epoch, ev) ->
                            post (fun () ->
                                if epoch = client.ConnectionGeneration then
                                    handler ev))

                    let subscribeClosed (handler: string option -> unit) : IDisposable =
                        client.ConnectionClosed.Subscribe(fun (epoch, error) ->
                            post (fun () ->
                                if epoch = client.ConnectionGeneration then
                                    handler (error |> Option.map (fun e -> e.Message))))

                    let closeWindow () : bool =
                        match mainWindow () with
                        | Some window ->
                            window.Close()
                            true
                        | None -> false

                    // ---- 视图组装（ShellViews）：参照 PWA 入口的 9 Desk 构造，桌面端共用同一批主题活实例 ----
                    let overlayLook () : Wanxiang2.OverlayDesk.OverlayDeskMetrics =
                        let theme = SharedTheme.manager
                        let brush k = theme.Brush k :> Avalonia.Media.IBrush
                        let metric m = theme.Metric m :> IObservable<float>

                        {
                            Layer =
                                {
                                    Scrim = brush Wanxiang2.ThemePalette.PaletteKey.Scrim
                                    Surface = brush Wanxiang2.ThemePalette.PaletteKey.Surface
                                    Edge = brush Wanxiang2.ThemePalette.PaletteKey.Border
                                    DialogShadow = Avalonia.Media.BoxShadows()
                                    PopupShadow = Avalonia.Media.BoxShadows()
                                    PopupGap = 4.0
                                    PopupMinWidth = 180.0
                                    ScrimFade = TimeSpan.Zero
                                    ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut()
                                }
                            Toasts =
                                {
                                    Surface = brush Wanxiang2.ThemePalette.PaletteKey.SurfaceRaised
                                    Edge = brush Wanxiang2.ThemePalette.PaletteKey.Border
                                    Text = brush Wanxiang2.ThemePalette.PaletteKey.Text
                                    Accent = fun _ -> brush Wanxiang2.ThemePalette.PaletteKey.Accent
                                    AccentOpacity = 0.6
                                    Padding = Thickness(16.0, 12.0)
                                    Shadow = Avalonia.Media.BoxShadows()
                                    LineHeight = 18.0
                                    ColumnSpacing = 12.0
                                    Spacing = 8.0
                                    Margin = Thickness(24.0, 0.0, 24.0, 32.0)
                                    MaxWidth = 480.0
                                    BodyMaxHeight = 160.0
                                    Fade = TimeSpan.Zero
                                    FadeEasing = Avalonia.Animation.Easings.CubicEaseOut()
                                }
                            Menu =
                                {
                                    Text = brush Wanxiang2.ThemePalette.PaletteKey.Text
                                    Muted = brush Wanxiang2.ThemePalette.PaletteKey.TextMuted
                                    Faint = brush Wanxiang2.ThemePalette.PaletteKey.TextFaint
                                    Danger = brush Wanxiang2.ThemePalette.PaletteKey.Danger
                                    Accent = brush Wanxiang2.ThemePalette.PaletteKey.Accent
                                    Check =
                                        fun b ->
                                            Wanxiang2.IconSet.IconSet.image
                                                Wanxiang2.IconSet.Glyph.Check
                                                b
                                                (metric Wanxiang2.ThemeMetrics.Metric.IconGlyph)
                                    Chevron =
                                        fun b ->
                                            Wanxiang2.IconSet.IconSet.image
                                                Wanxiang2.IconSet.Glyph.ChevronRight
                                                b
                                                (metric Wanxiang2.ThemeMetrics.Metric.IconGlyph)
                                    IconSlotWidth = 16.0
                                    ItemMinHeight = 32.0
                                    MaxHeight = 200.0
                                }
                            MenuMinWidth = 200.0
                            GroupedMenuMinWidth = 240.0
                            DwellFailureMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellFailure).Value
                            DwellWarningMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellWarning).Value
                            DwellDefaultMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellDefault).Value
                            DismissTick = TimeSpan.FromMilliseconds 100.0
                        }

                    /// 真正的组装（只跑一次；由 buildViews 记下结果）。
                    let buildViewsOnce () : ShellViews =
                        let theme = SharedTheme.manager
                        let styles = SharedTheme.styles
                        let root = Grid()
                        rootGrid <- root
                        // 输入方式（触屏 / 键鼠）挂在根控件上。不挂则 PrefersTouch 永远为假，
                        // 触屏上的菜单快捷键提示和悬停提示不会收起（PWA 已挂同一句）。
                        SharedTheme.input.Track root
                        // KeyShortcut 换算桥装配（迁自旧 Controls/KeyShortcut.fs 的路由）：
                        // 根视图 KeyDown → KeyChord → ShortcutKey → 意图 → ShellCommands 分派。
                        root.AddHandler(
                            Avalonia.Input.InputElement.KeyDownEvent,
                            EventHandler<Avalonia.Input.KeyEventArgs>(fun _ e ->
                                let action = KeyShortcut.resolve e

                                if action <> ShortcutAction.NoShortcut then
                                    e.Handled <- shell.HandleShortcut action)
                        )
                        // 决策 454：焦点不在本视图里时（点了画布空白处、PWA 首屏），按键只落到 TopLevel，
                        // 不冒泡经过本视图——顶层补一个入口，只处理来源不在本视图内的按键。
                        root.AttachedToVisualTree.Add(fun _ ->
                            match TopLevel.GetTopLevel root with
                            | null -> ()
                            | top when topLevelKeysHooked = Some top -> ()
                            | top ->
                                topLevelKeysHooked <- Some top

                                top.KeyDown.Add(fun e ->
                                    let inside =
                                        match e.Source with
                                        | :? Avalonia.Visual as v ->
                                            obj.ReferenceEquals(v, root)
                                            || Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(root, v)
                                        | _ -> false

                                    if not e.Handled && not inside then
                                        let action = KeyShortcut.resolve e

                                        if action <> ShortcutAction.NoShortcut then
                                            e.Handled <- shell.HandleShortcut action))

                        let ov = Wanxiang2.OverlayDesk.OverlayDesk(root, overlayLook ())
                        overlay <- ov
                        let dialogs = Wanxiang2.DialogsDesk.DialogsDesk(theme, ov)
                        let brandLogo = SharedTheme.logo

                        let composer =
                            Wanxiang2.ComposerDesk.ComposerDesk(
                                shell.ComposerActions(pickAttachment, dropFiles, pasteFromClipboard)
                            )

                        composerDesk <- composer
                        // 提示条的动作按钮持焦时条被移除（点动作、F6 后撤销、过期）：
                        // 焦点回输入区，别丢在已销毁的按钮上。
                        ov.WireFocusHome(fun () -> composerDesk.Focus())

                        let approval = Wanxiang2.ApprovalDesk.ApprovalDesk(theme, ov, send)

                        let tintHost: Wanxiang2.CodeTint.CodeTintHost =
                            {
                                HighlightHtml =
                                    fun (code, lang) ->
                                        match Wanxiang.UI.RichBackend.current () with
                                        | Some backend -> backend.HighlightHtml(code, lang)
                                        | None -> None
                                // 桌面着色跑在后台线程（旧 Reading.tint 的桌面分支）；
                                // 浏览器端才是「宿主 JS 引擎当场算」。
                                Background = fun work -> Task.Run(work) |> ignore
                                After = fun ms action -> after (TimeSpan.FromMilliseconds(float ms)) action |> ignore
                                Synchronous = false
                            }

                        let tint = Wanxiang2.CodeTint.CodeTint(tintHost)

                        let cards =
                            Wanxiang2.MessageCardView.MessageCards(
                                Wanxiang2.MessageCardView.CardLooks.ofTheme theme styles,
                                {
                                    Tint = tint
                                    MathHtml =
                                        fun (tex, display) ->
                                            match Wanxiang.UI.RichBackend.current () with
                                            | Some backend -> backend.MathHtml(tex, display)
                                            | None -> None
                                    Wrap = codeWrap
                                    Clock = Wanxiang2.MessageCardView.CaretClock()
                                    Touch = Wanxiang2.MessageCardView.CardTouch()
                                    Now = fun () -> DateTimeOffset.UtcNow
                                }
                            )

                        let chat =
                            Wanxiang2.ChatScreen.ChatScreen(
                                Wanxiang2.ChatScreen.ChatScreenMetrics.ofTheme theme styles,
                                cards,
                                composer,
                                approval,
                                ov,
                                brandLogo,
                                shell.ChatActions()
                            )
                        // 后台着色算完：卡片身份没变，必须显式丢缓存才会换成着色版（入口持有 tint 实例）。
                        tint.Ready.Add(fun _ -> post (fun () -> chat.InvalidateCards()))

                        let sidebar =
                            Wanxiang2.SidebarDesk.SidebarDesk(theme, ov, shell.SidebarActions(), brandLogo)

                        let settingsHooks: Wanxiang2.SettingsScreen.SettingsHooks = shell.SettingsHooks()

                        let settings =
                            Wanxiang2.SettingsScreen.SettingsScreen(
                                theme,
                                ov,
                                settingsHooks,
                                Wanxiang.UI.UiPrefsFile.load ()
                            )

                        let onboarding =
                            Wanxiang2.SettingsScreen.OnboardingDesk(theme, ov, shell.OnboardingHooks())

                        let workspace =
                            Wanxiang2.Workspace.Workspace(
                                theme,
                                root,
                                sidebar,
                                chat,
                                composer,
                                shell.WorkspaceHooks(fun () -> SharedTheme.input.PrefersTouch)
                            )

                        let palette =
                            Wanxiang2.PaletteDesk.PaletteDesk(theme, ov, fun () -> SharedTheme.input.PrefersTouch)
                        // 决策 452：每次执行后写回，不等关窗。崩溃或被杀时这次选择仍在。
                        palette.Run.Add(fun _ -> PaletteRecentFile.saveRecent (shell.PaletteRecent()))
                        let modelSwitch = Wanxiang2.ModelSwitchDesk.ModelSwitchDesk(theme, ov)

                        {
                            Sidebar = sidebar
                            Chat = chat
                            Composer = composer
                            Settings = settings
                            Onboarding = onboarding
                            Workspace = workspace
                            Approval = approval
                            Palette = palette
                            ModelSwitch = modelSwitch
                            Overlay = ov
                            Dialogs = dialogs
                            NewExportDesk =
                                fun sendQuery lineage conversationId title ->
                                    Wanxiang2.TransferDesk.ExportDesk(
                                        Wanxiang2.TransferDesk.TransferLook.exportOf theme,
                                        Wanxiang2.TransferDesk.TransferTiming.ofThemeWithAfter theme after,
                                        ov,
                                        conversationId,
                                        title,
                                        sendQuery,
                                        topLevel,
                                        lineage
                                    )
                            NewImportDesk =
                                fun submit ->
                                    Wanxiang2.TransferDesk.ImportDesk(
                                        Wanxiang2.TransferDesk.TransferLook.importOf theme,
                                        Wanxiang2.TransferDesk.TransferTiming.ofThemeWithAfter theme after,
                                        ov,
                                        topLevel,
                                        submit
                                    )
                            SettingsHostVisible = fun visible -> settings.View.IsVisible <- visible
                            WorkspaceHostVisible = fun visible -> workspace.Host.IsVisible <- visible
                            SettingsFocusInitial = fun () -> settings.FocusInitial()
                            SettingsShowSection =
                                fun section ->
                                    section
                                    |> Option.iter (fun s ->
                                        Wanxiang2.SettingsScreen.SettingsPart.ofKey s
                                        |> Option.iter settings.ShowSection)
                        }

                    // ---- 唯一副作用执行器（Effect Runner）雏形（重构架构规范）----
                    let desktopPrependAnchor: (float * float) option ref = ref None

                    let resolveDesktopFocus (target: FocusTarget) =
                        match builtViews with
                        | None -> ()
                        | Some views ->
                            match target with
                            | FocusTarget.ComposerInput -> views.Composer.Focus()
                            | FocusTarget.SidebarSearch -> views.Sidebar.FocusSearch()
                            | FocusTarget.DialogInitial -> views.SettingsFocusInitial()
                            | FocusTarget.DrawerToggle -> ()
                            | FocusTarget.Toast -> views.Overlay.FocusToastAction() |> ignore
                            | FocusTarget.Row _ -> ()
                            | FocusTarget.Exchange _ -> ()
                            | FocusTarget.None_ -> ()

                    let resolveDesktopScrollChat (cmd: ScrollCommand) =
                        match builtViews with
                        | None -> ()
                        | Some views ->
                            let viewer = views.Chat.Pane.Scroller.Viewer

                            match cmd with
                            | ScrollCommand.End -> views.Chat.SmoothScrollToEnd()
                            | ScrollCommand.Beginning -> views.Chat.SmoothScrollToBeginning()
                            | ScrollCommand.ToExchange msgId ->
                                // 定位目标（搜索命中 / 消息链接）：滚到那条消息并短暂高亮
                                match UInt64.TryParse msgId with
                                | true, commit -> views.Chat.RevealAndHighlight commit |> ignore
                                | false, _ -> ()
                            | ScrollCommand.PreparePrepend ->
                                desktopPrependAnchor.Value <- Some(viewer.Extent.Height, viewer.Offset.Y)
                            | ScrollCommand.RestorePrepend ->
                                match desktopPrependAnchor.Value with
                                | None -> ()
                                | Some(oldExtent, oldOffset) ->
                                    desktopPrependAnchor.Value <- None

                                    let newOffset =
                                        ScrollAnchorMath.computeRestoredOffset oldExtent oldOffset viewer.Extent.Height

                                    views.Chat.Pane.Scroller.PinOffset newOffset

                    let desktopEffectSink: EffectSink =
                        {
                            Focus = resolveDesktopFocus
                            ScrollChat = resolveDesktopScrollChat
                            SendWire = send
                            After =
                                fun (ms, action) ->
                                    after (TimeSpan.FromMilliseconds(float ms)) (fun () ->
                                        match action with
                                        | :? Wanxiang2.WorkspaceState.Msg as m ->
                                            if not (isNull (box shell)) then
                                                shell.Dispatch m
                                        | :? (unit -> unit) as fn -> fn ()
                                        | _ -> ())
                                    |> ignore
                            Every =
                                fun (_handle, ms) ->
                                    // handle 登记管理
                                    every (TimeSpan.FromMilliseconds(float ms)) (fun () -> ()) |> ignore
                            StopEvery = fun _handle -> ()
                            ShowToast =
                                fun (text, tone) ->
                                    let shellTone =
                                        match tone with
                                        | Wanxiang2.WorkspaceState.ToastTone.Success -> ShellToastTone.Success
                                        | Wanxiang2.WorkspaceState.ToastTone.Warning -> ShellToastTone.Warning
                                        | Wanxiang2.WorkspaceState.ToastTone.Failure -> ShellToastTone.Failure
                                        | _ -> ShellToastTone.Info

                                    toast text shellTone
                            Clipboard = copyToClipboard
                            OpenLink = openLink
                            SaveDownload = fun (f, b) -> saveDownload f b
                            ApplyTheme =
                                fun theme ->
                                    let isDark =
                                        match theme with
                                        | ThemeKind.Dark -> true
                                        | ThemeKind.Light -> false
                                        | ThemeKind.System -> systemPrefersDark ()

                                    SharedTheme.manager.Apply(
                                        if isDark then
                                            Wanxiang2.ThemePalette.PaletteMode.Dark
                                        else
                                            Wanxiang2.ThemePalette.PaletteMode.Light
                                    )
                            SetWindowBusy =
                                fun busy ->
                                    match mainWindow () with
                                    | Some win ->
                                        win.Cursor <-
                                            if busy then
                                                new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Wait)
                                            else
                                                null
                                    | None -> ()
                            CloseWindow = fun () -> closeWindow () |> ignore
                            PrefsSave = Wanxiang.UI.UiPrefsFile.save
                            PrefsLoad = fun () -> Wanxiang.UI.UiPrefsFile.load () |> ignore
                            DraftWrite = applyDraftWrite
                            BrowserHook = fun _ -> ()
                        }

                    let _ = desktopEffectSink

                    /// 视图组装入口（Build 与 Root 都调它）：只组装一次，重复调用返回同一批实例。
                    let buildViews () : ShellViews =
                        match builtViews with
                        | Some views -> views
                        | None ->
                            let views = buildViewsOnce ()
                            builtViews <- Some views
                            views

                    /// 根视图工厂（App.Root）：MainView 由 ShellComposer 驱动，这里给根 Grid。
                    let rootFactory () : Control =
                        if isNull rootGrid then
                            buildViews () |> ignore

                        rootGrid :> Control

                    let ports: ShellPorts =
                        {
                            Connect = connectAsync
                            SendAuth = sendAuth
                            Send = send
                            SendCommand = sendCommand
                            SendAtGeneration = sendAtGeneration
                            After = after
                            Every = every
                            Post = post
                            Toast = toast
                            ToastUndo = toastUndo
                            CopyToClipboard = copyToClipboard
                            OpenLink = openLink
                            SaveDownload = saveDownload
                            ApplyDraftWrite = applyDraftWrite
                            LoadLocalDraft =
                                fun () ->
                                    // 端口要求 Async；IDraftBackend 返回 Task，用 AwaitTask 转换。
                                    draftBackend.Load() |> Async.AwaitTask
                            PrefsLoad = Wanxiang.UI.UiPrefsFile.load
                            PrefsSave = Wanxiang.UI.UiPrefsFile.save
                            TryLoadClientToml = Wanxiang.UI.CredentialStore.tryLoadClientToml
                            TryLoadBrowserConnection =
                                fun () ->
                                    // 端口要求 Async；CredentialStore 返回 Task，用 AwaitTask 转换。
                                    Wanxiang.UI.CredentialStore.tryLoadBrowserConnectionAsync () |> Async.AwaitTask
                            SaveBrowserConnection = fun _ _ _ _ -> ()
                            BrowserViewportWidth = browserViewportWidth
                            BrowserResumeEpoch = browserResumeEpoch
                            BrowserPageUrl = browserPageUrl
                            BrowserConversationSucceeded = browserConversationSucceeded
                            BrowserAddHidden = browserAddHidden
                            SystemPrefersDark = systemPrefersDark
                            DefaultServerUrl = Wanxiang.UI.CredentialStore.defaultServerUrl
                            ApplyTheme =
                                fun dark ->
                                    SharedTheme.manager.Apply(
                                        if dark then
                                            Wanxiang2.ThemePalette.PaletteMode.Dark
                                        else
                                            Wanxiang2.ThemePalette.PaletteMode.Light
                                    )
                            SetReducedMotion = fun reduced -> SharedTheme.manager.Motion.ApplyReduced reduced
                            SetCodeWrap = fun wrapped -> codeWrap.Wrapped <- wrapped
                            CaptureFocus = captureFocus
                            IsEditingText = isEditingText
                            ConnectionGeneration = connectionGeneration
                            IsConnected = isConnected
                            SubscribeHostEvents = subscribeHostEvents
                            SubscribeClosed = subscribeClosed
                            CloseWindow = closeWindow
                            ClientName = fun () -> Wanxiang.UI.CredentialStore.clientName
                            MakeThumbnail = makeThumbnail
                            EffectSink = desktopEffectSink
                            BuildViews = buildViews
                        }

                    shell <- ShellComposer(ports)
                    // PaletteRecent 读写接线：启动时装入已存的最近使用（与 ui.json 同目录）。
                    shell.ApplyPaletteRecent(PaletteRecentFile.loadRecent ())
                    // App 静态端口（EntryShells）：Root 创建根视图、Build 调 ShellComposer.Build 并接事件、
                    // 关窗落草稿、偏好读写。
                    Wanxiang.UI.App.Root <- Some rootFactory

                    Wanxiang.UI.App.Build <-
                        Some(fun _view ->
                            every (TimeSpan.FromMilliseconds 250.0) (fun () ->
                                shell.Dispatch(Wanxiang2.WorkspaceState.Msg.MsgTick DateTimeOffset.UtcNow))
                            |> ignore

                            shell.Build() |> ignore
                            // 本机草稿（文件）读完再交给组合者：读完之前不写盘（与旧时序一致）。
                            // 系统深色变化：只有「跟随系统」时才重应用主题（组合者判断）。
                            match Application.Current with
                            | null -> ()
                            | app ->
                                match app.PlatformSettings with
                                | null -> ()
                                | settings -> settings.ColorValuesChanged.Add(fun _ -> post (fun () -> ()))

                            shell.AutoConnect())
                    // 关窗：先落草稿，再落面板最近使用（同一拍）。
                    Wanxiang.UI.App.FlushDrafts <- Some(fun () -> ())
                    Wanxiang.UI.App.LoadPrefs <- Some Wanxiang.UI.UiPrefsFile.load
                    Wanxiang.UI.App.SavePrefs <- Some Wanxiang.UI.UiPrefsFile.save
                    // 宿主 StartupTrace（决策 290）：采样退出由入口安装
                    //（旧宿主 MainWindow 的 requestExit = Environment.Exit 0）；退出时机归组合者
                    //（listReady 后 3 秒 / chatReady），首帧只打点不退出。
                    Wanxiang.UI.StartupTrace.requestExit <- fun () -> Environment.Exit 0

                    Wanxiang.UI.App.MarkFirstFrame <-
                        Some(fun () -> Wanxiang.UI.StartupTrace.mark "firstFrame" |> ignore)
                    // MainWindow 由 EntryShells 的 App.OnFrameworkInitializationCompleted 在
                    // IClassicDesktopStyleApplicationLifetime 分支构造（Root/Build/LoadPrefs/SavePrefs 齐备时）。
                    AppBuilder
                        .Configure<Wanxiang.UI.App>()
                        .UsePlatformDetect()
                        .StartWithClassicDesktopLifetime(
                            argv
                            |> Array.filter (fun a ->
                                not (a.StartsWith("wanxiang://", StringComparison.OrdinalIgnoreCase)))
                        )

                let code = runDesktopUi argv
                stopServer ()

                match sigterm with
                | Some r -> r.Dispose()
                | None -> ()

                code
            else
                // 无 UI：主机等待信号优雅关闭（决策 101/102）。
                // SIGTERM（kill 默认）与 SIGINT（Ctrl+C）都触发优雅关闭：第一次排空退出，第二次立即退出。
                use cts = new CancellationTokenSource()
                let signals = ShutdownSignals()

                let shutdownOnce () =
                    // 信号回调可能并发（SIGINT+SIGTERM 同时到达），ShutdownSignals 保证恰好一次 Graceful
                    match signals.Signal() with
                    | ShutdownStep.Force -> Environment.Exit ShutdownSignals.ForceExitCode
                    | ShutdownStep.Graceful ->
                        logInfo "shutting down gracefully (send signal again to force)"
                        stopServer ()
                        cts.Cancel()

                let ctrlHandler =
                    ConsoleCancelEventHandler(fun _ e ->
                        e.Cancel <- true
                        shutdownOnce ())

                Console.CancelKeyPress.AddHandler ctrlHandler

                let sigterm =
                    try
                        Some(
                            PosixSignalRegistration.Create(
                                PosixSignal.SIGTERM,
                                Action<PosixSignalContext>(fun ctx ->
                                    ctx.Cancel <- true
                                    shutdownOnce ())
                            )
                        )
                    with _ ->
                        None

                try
                    Task.Delay(Timeout.Infinite, cts.Token).Wait()
                with
                | :? AggregateException -> ()
                | :? OperationCanceledException -> ()

                stopServer ()

                match sigterm with
                | Some r -> r.Dispose()
                | None -> ()

                0
        with e ->
            eprintfn "wanxiang: %s" e.Message
            exit 1
