namespace Wanxiang.Pwa

open System
open System.IO
open System.Runtime.Versioning
open System.Threading
open System.Threading.Tasks
open Avalonia
open Avalonia.Browser
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Input.Platform
open Avalonia.Media.Imaging
open Avalonia.Platform
open Avalonia.Platform.Storage
open Avalonia.Threading
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.Interop
open Wanxiang.Server
open Wanxiang.UI

/// 万象 PWA 独立入口（wanxiang2 侧；PLAN.md GAP1 另一半）。
/// 编译目标 net10.0-browser（browser-wasm），由 wwwroot/main.js 引导并渲染进 #out 容器。
/// 不依赖宿主 src/ 任何文件，只消费 Wanxiang2.* 交付包。
///
/// 分工（与桌面入口同一套）：编排与状态归 ShellComposer（compose/AppShell），
/// 本入口只做 IO（文件选择 / 拖入 / 粘贴 / 保存 / 剪贴板 / 链接 / localStorage）、
/// 计时器、Avalonia 控件与 Desk 构造。浏览器端口全部走 Avalonia 自带的实现
/// （TopLevel.Clipboard / Launcher / StorageProvider、PlatformSettings、DragDrop），
/// 不新增任何 JS 桥（wwwroot/main.js 的 18 个导入未变）。
module Program =

    [<assembly: SupportedOSPlatform("browser")>]
    do ()

    /// 决策 452：小型本机文本（界面偏好 ui.json、命令面板最近使用）在浏览器里落 localStorage，
    /// 键 = `"wanxiang.local:" + Path.GetFileName path`（与旧宿主 `LocalText` 逐字一致）。
    /// 桌面对应物是文件（UiPrefsFile / PaletteRecentFile）；这里只做浏览器落点。
    module private LocalText =

        let private key (path: string) =
            "wanxiang.local:" + IO.Path.GetFileName path

        /// 读取；无则 None（localStorage 不可用同样当没有）。
        let read (path: string) : string option =
            try
                match BrowserLocalStore.Get(key path) with
                | null -> None
                | text -> Some text
            with _ ->
                None

        /// 写入；返回是否写到了持久存储。
        let write (path: string) (text: string) : bool =
            try
                BrowserLocalStore.Set(key path, text)
                true
            with _ ->
                false

    // ---- 计时（浏览器端 DispatcherTimer，UI 线程）----
    let private every (span: TimeSpan) (action: unit -> unit) : IDisposable =
        let timer = DispatcherTimer(Interval = span)
        timer.Tick.Add(fun _ -> action ())
        timer.Start()

        { new IDisposable with
            member _.Dispose() = timer.Stop()
        }

    let private after (span: TimeSpan) (action: unit -> unit) : IDisposable =
        let timer = DispatcherTimer(Interval = span)

        timer.Tick.Add(fun _ ->
            timer.Stop()
            action ())

        timer.Start()

        { new IDisposable with
            member _.Dispose() = timer.Stop()
        }

    // ---- WebSocket 客户端（WsClient）----
    let private ws = WsClient()
    let mutable private connectionEpoch = 0

    /// 连接（地址由组合者给出：保存的凭据或连接对话框里输入的地址）；返回连接代次。
    let private connectAsync (url: string) : Async<int> =
        async {
            let! epoch =
                ws.ConnectWithGenerationAsync(Uri url, Threading.CancellationToken.None)
                |> Async.AwaitTask

            connectionEpoch <- epoch
            return epoch
        }

    /// 认证事件只发在建立它的那一代连接上（换主机后的旧认证绝不落到新连接）。
    let private sendAuth (epoch: int) (ev: WireEvent) =
        if epoch = connectionEpoch then
            ws.TrySendAtGenerationAsync(epoch, ev) |> ignore

    let private send (ev: WireEvent) = ws.SendAsync ev |> ignore

    /// 命令发送：返回是否真的发出（组合者据此标记未确认与回执等待）。
    let private sendCommand (cmd: ClientCommand) : Task<bool> = ws.TrySendCommandAsync cmd

    /// 按当前连接代次发一条协议事件（导出分页读取用）；返回是否真的发出。
    let private sendAtGeneration (ev: WireEvent) : Task<bool> =
        ws.TrySendAtGenerationAsync(ws.ConnectionGeneration, ev)

    /// 把回调投递到 UI 线程（后台续体不直接碰控件）。
    let private post (action: unit -> unit) = Dispatcher.UIThread.Post(Action action)

    // ---- 浏览器端口（Wanxiang.Interop.BrowserPage / BrowserLocalStore）----
    let private browserViewportWidth () = BrowserPage.ViewportWidth()
    let private browserResumeEpoch () = BrowserPage.ResumeEpoch()
    let private browserPageUrl () = BrowserPage.Url()
    let private browserConversationSucceeded () = BrowserPage.ConversationSucceeded()

    let private browserAddHidden (handler: unit -> unit) =
        BrowserPage.add_Hidden (fun () -> handler ())

    // ---- 视图与组合者 ----
    // 视图工厂要先拿到动作记录（工厂方法在组合者上），所以组合者在根视图工厂里先建好；
    // 端口回调（缩略图 / 剪贴板 / 文件 IO）都要在运行时读这几个活实例。
    let mutable private overlay: Wanxiang2.OverlayDesk.OverlayDesk =
        Unchecked.defaultof<_>

    let mutable private composerDesk: Wanxiang2.ComposerDesk.ComposerDesk =
        Unchecked.defaultof<_>

    let mutable private chatDesk: Wanxiang2.ChatScreen.ChatScreen =
        Unchecked.defaultof<_>

    let mutable private paletteDesk: Wanxiang2.PaletteDesk.PaletteDesk =
        Unchecked.defaultof<_>

    let mutable private tint: Wanxiang2.CodeTint.CodeTint = Unchecked.defaultof<_>
    /// 代码块折行的宿主侧记忆（与 MessageCards 共享同一实例）。
    let private wrapMemory = Wanxiang2.MarkdownView.CodeWrapMemory(false)
    let mutable private shell: ShellComposer = Unchecked.defaultof<_>
    let mutable private rootGrid: Grid = null
    let mutable private started = false
    /// 顶层按键入口是否已挂上（决策 454 的 TopLevel 那一半）。
    let mutable private topKeysHooked = false
    /// 组装好的视图（Root 与 Build 都经 buildViews，只组装一次）。
    let mutable private builtViews: ShellViews option = None

    /// 入口持有的 TopLevel（浏览器没有 Window：根视图即容器）。
    let private topLevel () : TopLevel =
        if isNull rootGrid then
            null
        else
            TopLevel.GetTopLevel rootGrid

    // ---- 提示条（OverlayDesk）----
    let private toast (text: string) (tone: ShellToastTone) =
        match tone with
        | ShellToastTone.Info -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Info)
        | ShellToastTone.Success -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Success)
        | ShellToastTone.Warning -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Warning)
        | ShellToastTone.Failure -> overlay.Toast(text, Wanxiang2.ToastStrip.ToastTone.Failure)

    let private toastUndo (text: string) (undo: unit -> unit) =
        overlay.ToastWith(text, Wanxiang2.ToastStrip.ToastTone.Info, Some("撤销", undo))

    // ---- 主题：与桌面同一条路径（Avalonia 的 PlatformSettings / ThemeManager）----

    /// 平台的深色偏好。浏览器由 Avalonia 的 PlatformSettings 提供（prefers-color-scheme 经 matchMedia），
    /// 桌面来自系统设置——同一处读取。不能用 Application.ActualThemeVariant：它被固定为 Light 以稳定 Fluent 模板。
    let private systemPrefersDark () : bool =
        try
            match Application.Current with
            | null -> false
            | app ->
                match app.PlatformSettings with
                | null -> false
                | settings -> settings.GetColorValues().ThemeVariant = PlatformThemeVariant.Dark
        with _ ->
            false

    /// 主题：dark = true → Dark（组合者传 UiPrefsFile.resolveTheme 的结果）。
    let private applyTheme (dark: bool) =
        SharedTheme.manager.Apply(
            if dark then
                Wanxiang2.ThemePalette.PaletteMode.Dark
            else
                Wanxiang2.ThemePalette.PaletteMode.Light
        )

    /// 减弱动效开关（组合者按偏好调用）。
    let private setReducedMotion (value: bool) =
        SharedTheme.manager.Motion.ApplyReduced value

    /// 代码折行偏好：转交构造 MessageCards 时用的 CodeWrapMemory 实例（渲染期读取的默认值）。
    let private setCodeWrap (value: bool) = wrapMemory.Wrapped <- value

    /// 当前连接代次 / 连接是否活着（组合者的时间旅行判断与发送门控）。
    let private connectionGeneration () : int = ws.ConnectionGeneration
    let private isConnected () : bool = ws.IsConnected

    /// 订阅协议事件（组合者的界面面）。WsClient 内部已按连接代次过滤（TextSocket.IsCurrent），
    /// 这里只负责 marshal 回 UI 线程。
    let private subscribeHostEvents (handler: WireEvent -> unit) : IDisposable =
        ws.ConnectionEventReceived
        |> Observable.subscribe (fun (epoch, ev) ->
            if epoch = ws.ConnectionGeneration then
                Dispatcher.UIThread.Post(fun () -> handler ev))

    /// 订阅连接关闭：同样只认当前代次（换主机后旧连接的关闭不再报），错误说明转成文本。
    let private subscribeClosed (handler: string option -> unit) : IDisposable =
        ws.ConnectionClosed
        |> Observable.subscribe (fun (epoch, error) ->
            if epoch = ws.ConnectionGeneration then
                Dispatcher.UIThread.Post(fun () -> handler (error |> Option.map (fun e -> e.Message))))

    /// 图片缩略图：解码 bytes → IImage 交给输入区；解码失败忽略（旧 Composer.SetThumbnail 同路径）。
    let private makeThumbnail (attachmentId: Guid) (bytes: byte[]) =
        try
            use stream = new MemoryStream(bytes)
            composerDesk.ApplyThumbnail(attachmentId, Bitmap.DecodeToWidth(stream, 96))
        with _ ->
            ()

    /// 焦点是否落在有文字的输入框里（决策 232：Ctrl+Z 这时让位给文本框自己的撤销；
    /// 空输入框不算，撤销提示条仍然接管）。
    let private isEditingText () : bool =
        try
            match topLevel () with
            | null -> false
            | top ->
                match top.FocusManager with
                | null -> false
                | manager ->
                    match manager.GetFocusedElement() with
                    | :? TextBox as box -> not (String.IsNullOrEmpty box.Text)
                    | _ -> false
        with _ ->
            false

    /// 关窗：浏览器没有窗口（决策 449/451 的对应物是页面转入后台），恒 false。
    let private closeWindow () : bool = false

    /// 客户端标识（配对时上报；浏览器是 "PWA"）。
    let private clientName () : string = CredentialStore.clientName

    // ---- 剪贴板 / 链接 / 下载（Avalonia 自带实现，与桌面同一套 API）----

    /// 剪贴板写入可能因权限、焦点或宿主被拒：等写入落地再报，成功与失败各自说话。
    let private copyToClipboard (text: string) =
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
                            Dispatcher.UIThread.Post(fun () -> toast "已复制到剪贴板" ShellToastTone.Success)
                        with _ ->
                            Dispatcher.UIThread.Post(fun () -> toast "复制到剪贴板失败，请重试。" ShellToastTone.Failure)
                    }
                    |> Async.Start
        with _ ->
            ()

    /// 系统浏览器打开链接（wanxiang:// 的站内链接由组合者先行拦截，走不到这里）。
    let private openLink (url: string) =
        try
            match topLevel () with
            | null -> ()
            | top -> top.Launcher.LaunchUriAsync(Uri url) |> ignore
        with _ ->
            toast (sprintf "无法打开链接：%s" url) ShellToastTone.Warning

    /// 下载保存：保存对话框（浏览器是下载）→ 写字节；取消不发提示。
    let private saveDownload (fileName: string) (bytes: byte[]) =
        match topLevel () with
        | null -> ()
        | top ->
            async {
                try
                    let! file =
                        top.StorageProvider.SaveFilePickerAsync(FilePickerSaveOptions(SuggestedFileName = fileName))
                        |> Async.AwaitTask

                    if not (isNull file) then
                        use! stream = file.OpenWriteAsync() |> Async.AwaitTask
                        do! stream.WriteAsync(bytes, 0, bytes.Length) |> Async.AwaitTask

                        Dispatcher.UIThread.Post(fun () ->
                            toast (UploadFacts.savedText fileName) ShellToastTone.Success)
                with ex ->
                    Dispatcher.UIThread.Post(fun () ->
                        toast (UploadFacts.saveFailedText ex.Message) ShellToastTone.Failure)
            }
            |> Async.Start

    // ---- 附件入口（选择 / 拖入 / 粘贴）：读完一个文件就交给组合者一次 ----

    /// 读一个存储文件（浏览器是 File 对象）；读完即交回组合者，别在入口攒着。
    let private readStorageFile (file: IStorageFile) : Async<byte[]> =
        async {
            use! stream = file.OpenReadAsync() |> Async.AwaitTask
            use buffer = new MemoryStream()
            do! stream.CopyToAsync buffer |> Async.AwaitTask
            return buffer.ToArray()
        }

    /// 选文件 → 逐个读字节 → 每读完一项立刻 `AttachFiles`（上传归属判断与失败文案在组合者）；
    /// 读完（或失败）后焦点回输入区。
    let private pickFiles () =
        match topLevel () with
        | null -> ()
        | top ->
            // 归属在打开选择窗口前取：用户在选择窗口里的期间换主机 / 重连，读完就不上传。
            let owner = shell.UploadOwner()

            FilePick.startOnCaller
                (fun () ->
                    top.StorageProvider.OpenFilePickerAsync(
                        FilePickerOpenOptions(
                            AllowMultiple = true,
                            Title = "添加附件",
                            FileTypeFilter =
                                [
                                    for (label, patterns, mimes) in MediaTypes.pickerFilters ->
                                        FilePickerFileType(label, Patterns = patterns, MimeTypes = mimes)
                                ]
                        )
                    ))
                (fun started ->
                    async {
                        try
                            let! picked = started |> Async.AwaitTask

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
                    |> Async.Start)

    /// 拖入 / 粘贴来的文件项：`IStorageFile` 直接读流；否则退回本地路径。逐项交回组合者。
    let private dropFiles (items: IStorageItem list) =
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

    /// Ctrl/Cmd+V：剪贴板里有文件就先接管（转拖入路径），有位图就编码成 PNG；
    /// 都没有 → false，让文本框走默认粘贴。
    let private pasteFromClipboard () : bool =
        match topLevel () with
        | null -> false
        | top ->
            try
                let clipboard = top.Clipboard

                if isNull clipboard then
                    false
                else
                    let formatsTask = clipboard.GetDataFormatsAsync()

                    if formatsTask.Wait(100) && not (isNull formatsTask.Result) then
                        let formats = formatsTask.Result
                        let hasFiles = formats |> Seq.exists (fun f -> f = DataFormat.File)
                        let hasBitmap = formats |> Seq.exists (fun f -> f = DataFormat.Bitmap)

                        match UploadFacts.pasteSource hasFiles hasBitmap with
                        | UploadFacts.PasteSource.Files ->
                            async {
                                try
                                    let! files = clipboard.TryGetFilesAsync() |> Async.AwaitTask

                                    if not (isNull files) && files.Length > 0 then
                                        post (fun () -> dropFiles (List.ofArray files))
                                with ex ->
                                    post (fun () ->
                                        toast (UploadFacts.clipboardFilesFailedText ex.Message) ShellToastTone.Failure
                                        composerDesk.Focus())
                            }
                            |> Async.Start

                            true
                        | UploadFacts.PasteSource.Bitmap ->
                            let owner = shell.UploadOwner()

                            async {
                                try
                                    let! bitmap = clipboard.TryGetBitmapAsync() |> Async.AwaitTask

                                    if not (isNull bitmap) then
                                        use buffer = new MemoryStream()
                                        bitmap.Save(buffer, PngBitmapEncoderOptions())
                                        let bytes = buffer.ToArray()
                                        let name = UploadFacts.pasteFileName DateTime.Now

                                        post (fun () ->
                                            shell.AttachFiles owner [ (name, bytes) ]
                                            composerDesk.Focus())
                                with ex ->
                                    post (fun () ->
                                        toast (UploadFacts.clipboardImageFailedText ex.Message) ShellToastTone.Failure
                                        composerDesk.Focus())
                            }
                            |> Async.Start

                            true
                        | UploadFacts.PasteSource.NotHandled -> false
                    else
                        false
            with _ ->
                false

    /// 捕获当前焦点并返回「恢复焦点」闭包；控件已不可用（禁用 / 不可见 / 已离树）时返回 false，
    /// 由组合者回退到输入区（旧 CurrentFocusControl / RestoreSettingsFocus）。
    let private captureFocus () : (unit -> bool) =
        let captured =
            try
                match topLevel () with
                | null -> None
                | top ->
                    match top.FocusManager with
                    | null -> None
                    | manager ->
                        match manager.GetFocusedElement() with
                        | :? Control as control -> Some control
                        | _ -> None
            with _ ->
                None

        match captured with
        | None -> fun () -> false
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

    // ---- 本地草稿（浏览器 IndexedDB / LocalStore）----
    /// 草稿落点经 BrowserDraftBackend（IDraftBackend 的 IndexedDB 实现，异常吞并与根 src 一致）；
    /// 接口由 wanxiang2 libs 层定义（签名与根 src 逐字一致），这里只提供浏览器实现并接线写 / 删。
    let private draftBackend: IDraftBackend = BrowserDraftBackend() :> IDraftBackend

    let private applyDraftWrite (write: DraftWrite) =
        match write with
        | DraftWrite.Unchanged -> ()
        | DraftWrite.Write text -> draftBackend.Save text |> ignore
        | DraftWrite.Delete -> draftBackend.Clear() |> ignore

    // ---- 本机偏好与命令面板「最近使用」（决策 452：localStorage，键带文件名）----

    /// 偏好：ui.json 的浏览器落点（键 `wanxiang.local:ui.json`）。
    /// 渲染时带上同键旧文本，未知键不丢（与 UiPrefsFile.save 传 readText() 同义）。
    let private prefsLoad () : UiPrefs =
        UiPrefs.parse UiPrefsFile.limits (LocalText.read (UiPrefsFile.filePath ()))

    let private prefsSave (prefs: UiPrefs) =
        let path = UiPrefsFile.filePath ()
        LocalText.write path (UiPrefs.render (LocalText.read path) prefs) |> ignore

    /// 命令面板「最近使用」：与 ui.json 同目录（文件名归 PaletteRecent），浏览器同样走 LocalText。
    let private paletteRecentPath () =
        Path.Combine(Path.GetDirectoryName(UiPrefsFile.filePath ()), PaletteRecent.fileName)

    let private paletteRecentLoad () : string list =
        LocalText.read (paletteRecentPath ())
        |> Option.map PaletteRecent.decode
        |> Option.defaultValue []

    let private paletteRecentSave (recent: string list) =
        LocalText.write (paletteRecentPath ()) (PaletteRecent.encode recent) |> ignore

    // ---- 凭据（浏览器 IndexedDB，经 BrowserCredentials / CredentialStore）----
    let private tryLoadClientToml () : (string * string) option = None

    let private tryLoadBrowserConnection () : Async<(string * string * string) option> =
        CredentialStore.tryLoadBrowserConnectionAsync () |> Async.AwaitTask

    let private saveBrowserConnection (instanceId: string) (url: string) (token: string) (name: string) =
        CredentialStore.saveBrowserConnectionAsync instanceId url token name |> ignore

    // ---- 视图组装（ShellViews）：全部 Desk 用主题活实例构造 ----
    let private overlayLook () : Wanxiang2.OverlayDesk.OverlayDeskMetrics =
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

    /// 各 Desk 的动作记录全部来自组合者（同一套行为文案与命令），入口不再自己接回调；
    /// 只有三个真正读 IO 的入口（选文件 / 拖入 / 粘贴）以闭包注入。
    /// 真正的组装：只跑一次（App.Root 与 ShellComposer.Build 都要视图，重复组装会挂出两套控件树）。
    let private buildViewsOnce () : ShellViews =
        let theme = SharedTheme.manager
        let styles = SharedTheme.styles
        let root = Grid()
        rootGrid <- root
        // 浏览器端没有系统字体：整棵可视树必须显式用内嵌的 Sarasa，
        // 否则所有没单独指定字体的控件（标签、侧栏、引导页…）的中文都是豆腐块。
        // `TextElement.FontFamilyProperty` 是 inherits 的附加属性，设在根上整棵树继承。
        root.SetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty, SharedTheme.manager.FontFamily.Value)
        |> ignore
        // 输入方式（触屏 / 键鼠）的隧道监听挂在根控件上（旧 InputSense.Track MainView）。
        SharedTheme.input.Track root
        let ov = Wanxiang2.OverlayDesk.OverlayDesk(root, overlayLook ())
        overlay <- ov
        let brandLogo = SharedTheme.logo

        let composer =
            Wanxiang2.ComposerDesk.ComposerDesk(shell.ComposerActions(pickFiles, dropFiles, pasteFromClipboard))

        composerDesk <- composer
        // 提示条的动作按钮持焦时条被移除（点动作、F6 后撤销、过期）：
        // 焦点回输入区，别丢在已销毁的按钮上。
        ov.WireFocusHome(fun () -> composerDesk.Focus())

        let approval = Wanxiang2.ApprovalDesk.ApprovalDesk(theme, ov, send)

        let tintHost: Wanxiang2.CodeTint.CodeTintHost =
            {
                HighlightHtml =
                    fun (code, lang) ->
                        match RichBackend.current () with
                        | Some backend -> backend.HighlightHtml(code, lang)
                        | None -> None
                Background = fun work -> work ()
                After = fun ms action -> after (TimeSpan.FromMilliseconds(float ms)) action |> ignore
                Synchronous = true
            }

        let codeTint = Wanxiang2.CodeTint.CodeTint(tintHost)
        tint <- codeTint

        let cards =
            Wanxiang2.MessageCardView.MessageCards(
                Wanxiang2.MessageCardView.CardLooks.ofTheme theme styles,
                {
                    Tint = codeTint
                    MathHtml =
                        fun (tex, display) ->
                            match RichBackend.current () with
                            | Some backend -> backend.MathHtml(tex, display)
                            | None -> None
                    Wrap = wrapMemory
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

        chatDesk <- chat

        let sidebar =
            Wanxiang2.SidebarDesk.SidebarDesk(theme, ov, shell.SidebarActions(), brandLogo)

        let settings =
            Wanxiang2.SettingsScreen.SettingsScreen(theme, ov, shell.SettingsHooks(), prefsLoad ())

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

        paletteDesk <- palette
        let modelSwitch = Wanxiang2.ModelSwitchDesk.ModelSwitchDesk(theme, ov)
        let dialogs = Wanxiang2.DialogsDesk.DialogsDesk(theme, ov)

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
            SettingsHostVisible =
                fun visible ->
                    if visible then
                        // 插到索引 0：设置页是整页宿主，必须排在覆盖层（遮罩 / 对话框卡 / 浮层卡 / 提示条）
                        // 之下，否则设置页自己打开的下拉菜单与确认框会被整页盖住。
                        if not (root.Children.Contains settings.View) then
                            root.Children.Insert(0, settings.View)
                    else
                        root.Children.Remove settings.View |> ignore

                    settings.View.IsVisible <- visible
            WorkspaceHostVisible = fun visible -> workspace.Host.IsVisible <- visible
            SettingsFocusInitial = fun () -> settings.FocusInitial()
            SettingsShowSection =
                fun section ->
                    section
                    |> Option.iter (fun s ->
                        Wanxiang2.SettingsScreen.SettingsPart.ofKey s
                        |> Option.iter settings.ShowSection)
        }

    // ---- 唯一副作用执行器（Effect Runner）雏形（浏览器端）----
    let private pwaPrependAnchor: (float * float) option ref = ref None

    let private resolvePwaFocus (target: FocusTarget) =
        if not (isNull (box composerDesk)) && not (isNull (box chatDesk)) then
            match target with
            | FocusTarget.ComposerInput -> composerDesk.Focus()
            | FocusTarget.SidebarSearch -> ()
            | FocusTarget.DialogInitial -> ()
            | FocusTarget.DrawerToggle -> ()
            | FocusTarget.Toast ->
                if not (isNull (box overlay)) then
                    overlay.FocusToastAction() |> ignore
            | FocusTarget.Row _ -> ()
            | FocusTarget.Exchange _ -> ()
            | FocusTarget.None_ -> ()

    let private resolvePwaScrollChat (cmd: ScrollCommand) =
        if not (isNull (box chatDesk)) then
            let viewer = chatDesk.Pane.Scroller.Viewer

            match cmd with
            | ScrollCommand.End -> chatDesk.SmoothScrollToEnd()
            | ScrollCommand.Beginning -> chatDesk.SmoothScrollToBeginning()
            | ScrollCommand.ToExchange msgId ->
                // 定位目标（搜索命中 / 消息链接）：滚到那条消息并短暂高亮
                match UInt64.TryParse msgId with
                | true, commit -> chatDesk.RevealAndHighlight commit |> ignore
                | false, _ -> ()
            | ScrollCommand.PreparePrepend -> pwaPrependAnchor.Value <- Some(viewer.Extent.Height, viewer.Offset.Y)
            | ScrollCommand.RestorePrepend ->
                match pwaPrependAnchor.Value with
                | None -> ()
                | Some(oldExtent, oldOffset) ->
                    pwaPrependAnchor.Value <- None

                    let newOffset =
                        ScrollAnchorMath.computeRestoredOffset oldExtent oldOffset viewer.Extent.Height

                    chatDesk.Pane.Scroller.PinOffset newOffset

    let private pwaEffectSink: EffectSink =
        {
            Focus = resolvePwaFocus
            ScrollChat = resolvePwaScrollChat
            SendWire = send
            SendCommand = fun cmd -> sendCommand cmd |> ignore
            After =
                fun (ms, action) ->
                    after (TimeSpan.FromMilliseconds(float ms)) (fun () ->
                        match action with
                        | :? (unit -> unit) as fn -> fn ()
                        | _ -> ())
                    |> ignore
            Every = fun (_handle, ms) -> every (TimeSpan.FromMilliseconds(float ms)) (fun () -> ()) |> ignore
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
            SetWindowBusy = fun _ -> ()
            CloseWindow = fun () -> ()
            PrefsSave = prefsSave
            PrefsLoad = fun () -> prefsLoad () |> ignore
            BrowserHook = fun _ -> ()
        }

    /// 视图组装入口（Root 与 Build 都调它）：只组装一次，重复调用返回同一批实例。
    let private buildViews () : ShellViews =
        match builtViews with
        | Some views -> views
        | None ->
            let views = buildViewsOnce ()
            builtViews <- Some views
            views

    /// ShellPorts 全字段。
    let private ports: ShellPorts =
        {
            Connect = connectAsync
            SendAuth = sendAuth
            Send = send
            SendCommand = sendCommand
            SendAtGeneration = sendAtGeneration
            After = after
            Every = every
            Now = fun () -> DateTimeOffset.UtcNow
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
            PrefsLoad = prefsLoad
            PrefsSave = prefsSave
            TryLoadClientToml = tryLoadClientToml
            TryLoadBrowserConnection = tryLoadBrowserConnection
            SaveBrowserConnection = saveBrowserConnection
            BrowserViewportWidth = browserViewportWidth
            BrowserResumeEpoch = browserResumeEpoch
            BrowserPageUrl = browserPageUrl
            BrowserConversationSucceeded = browserConversationSucceeded
            BrowserAddHidden = browserAddHidden
            SystemPrefersDark = systemPrefersDark
            DefaultServerUrl = CredentialStore.defaultServerUrl
            ApplyTheme = applyTheme
            SetReducedMotion = setReducedMotion
            SetCodeWrap = setCodeWrap
            CaptureFocus = captureFocus
            IsEditingText = isEditingText
            ConnectionGeneration = connectionGeneration
            IsConnected = isConnected
            SubscribeHostEvents = subscribeHostEvents
            SubscribeClosed = subscribeClosed
            MakeThumbnail = makeThumbnail
            ProbeDraft =
                fun url key kind done' ->
                    // 保存前探活走主机同一条请求。密钥只进请求头，回调回到 UI 线程。
                    let provider =
                        {
                            id = "draft"
                            kind = kind
                            label = ""
                            baseUrl = url
                            apiKey = key
                            models = []
                            defaultModel = ""
                            timeoutSeconds = 30
                            maxRetries = 0
                            enabled = true
                            promptCaching = false
                            headers = Map.empty
                            extraJson = None
                            visionOverrides = Map.empty
                        }

                    task {
                        let! result = ProviderCatalog.probeModels provider CancellationToken.None

                        let ok, models, error =
                            match result with
                            | Ok ids -> true, ids, None
                            | Error message -> false, [], Some message

                        post (fun () -> done' (ok, models, error))
                    }
                    |> ignore
            CloseWindow = closeWindow
            ClientName = clientName
            EffectSink = pwaEffectSink
            BuildViews = buildViews
        }

    /// 组合者：视图构造要先拿动作记录，所以在根视图工厂里先建好（构造只读偏好，不碰控件）。
    let private ensureShell () : ShellComposer =
        let _ = pwaEffectSink

        if obj.ReferenceEquals(shell, null) then
            shell <- ShellComposer(ports)

        shell

    /// 顶层按键入口那一半（决策 454）：焦点不在本视图里时（PWA 首屏、点了画布空白处），
    /// 按键只落到 TopLevel，不会冒泡经过根视图。
    let private hookTopLevelKeys () =
        if not topKeysHooked then
            match TopLevel.GetTopLevel rootGrid with
            | null -> ()
            | top ->
                topKeysHooked <- true

                top.KeyDown.Add(fun e ->
                    let inside =
                        match e.Source with
                        | :? Visual as v ->
                            obj.ReferenceEquals(v, rootGrid)
                            || Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(rootGrid, v)
                        | _ -> false

                    if
                        not e.Handled
                        && not inside
                        && shell.HandleShortcut(Wanxiang.UI.KeyShortcut.resolve e)
                    then
                        e.Handled <- true)

    /// 快捷键：根视图这一半照旧（本视图内的按键），顶层那一半只处理来源不在根视图里的按键，
    /// 同一次按键不会走两条路。
    let private installShortcuts () =
        rootGrid.KeyDown.Add(fun e ->
            if not e.Handled && shell.HandleShortcut(Wanxiang.UI.KeyShortcut.resolve e) then
                e.Handled <- true)
        // 根视图此刻通常已挂上（PWA 先把视图交给 SingleView 再 Build）；
        // 万一还没拿到 TopLevel，等挂载事件再补一次。
        if isNull (TopLevel.GetTopLevel rootGrid) then
            rootGrid.AttachedToVisualTree.Add(fun _ -> hookTopLevelKeys ())
        else
            hookTopLevelKeys ()

    /// 系统深色变化（prefers-color-scheme）：交给组合者（只有 prefs.theme = 跟随系统 才重应用主题）。
    let private subscribeSystemTheme () =
        match Application.Current with
        | null -> ()
        | app ->
            match app.PlatformSettings with
            | null -> ()
            | _ -> ()

    /// 后台着色算完：卡片身份没变，必须显式丢缓存才会换成着色版。
    let private subscribeTintReady () =
        tint.Ready.Add(fun _ -> Dispatcher.UIThread.Post(fun () -> chatDesk.InvalidateCards()))

    /// 命令面板「最近使用」的持久化（决策 452）：启动装入，每次执行条目后与页面转入后台时写回。
    let private subscribePaletteRecent () =
        paletteDesk.Run.Add(fun _ -> paletteRecentSave (shell.PaletteRecent()))
        BrowserPage.add_Hidden (fun () -> paletteRecentSave (shell.PaletteRecent()))

    /// App.Build：视图挂载后调用 ShellComposer.Build（协议事件、连接关闭由组合者经端口自行订阅）。
    let private buildApp (_view: Control) =
        if not started then
            started <- true
            let composer = ensureShell ()
            // 命令面板「最近使用」：Build 之前装入（Build 里就要渲染面板条目）。
            composer.ApplyPaletteRecent(paletteRecentLoad ())
            composer.Build() |> ignore
            installShortcuts ()
            subscribeSystemTheme ()
            subscribeTintReady ()
            subscribePaletteRecent ()
            // 浏览器没有 Window.Opened。根视图挂上后的下一帧就是 firstFrame。
            // exit 模式不在这一帧退出；组合者在 listReady / chatReady 调 requestExit。
            StartupTrace.requestExit <- fun () -> Environment.Exit 0

            Dispatcher.UIThread.Post(
                Action(fun () -> StartupTrace.mark "firstFrame" |> ignore),
                DispatcherPriority.Render
            )

        shell.AutoConnect()

    /// 根视图工厂（App.Root）：MainView 由 ShellComposer 驱动，这里给根 Grid。
    let private rootFactory () : Control =
        if isNull rootGrid then
            ensureShell () |> ignore
            buildViews () |> ignore

        rootGrid :> Control

    [<EntryPoint>]
    let main _argv =
        task {
            // 强制 WebGL（不准软渲染）：WebGL2 → WebGL1，不回退 Software2D。
            let options =
                BrowserPlatformOptions(
                    RenderingMode = [| BrowserRenderingMode.WebGL2; BrowserRenderingMode.WebGL1 |],
                    FrameworkAssetPathResolver = fun fileName -> $"./{fileName}?v=20261012b"
                )
            // 代码着色与公式排版交给浏览器自带的 JS 引擎；脚本正文仍是 .NET 侧的内嵌资源
            let rich, backend = RichBrowserAdapter.create ()
            RichBackend.install backend
            rich.Warmup()
            // 决策 342：图片缩小交给浏览器（原生解码 + Canvas，异步），不占 wasm 单线程。
            // 宿主版接线：Wanxiang2.ImageShrinkIo 提供 platformShrinker 槽位（与 Wanxiang2.ImageShrink
            // 的 ImageShrink 模块按名合并；shrinkAsync 优先走平台实现，None 回退包内 Skia）。
            ImageShrink.platformShrinker <- Some BrowserImageShrink.shrink
            // App 静态端口（EntryShells.App）：PWA 分支只挂 SingleView。
            App.Root <- Some rootFactory
            App.Build <- Some buildApp

            App.FlushDrafts <-
                Some(fun () ->
                    if not (obj.ReferenceEquals(shell, null)) then
                        // 关窗（浏览器：页面转入后台）先落草稿，再落面板「最近使用」，同一拍。
                        shell.FlushDrafts()
                        paletteRecentSave (shell.PaletteRecent()))

            App.LoadPrefs <- Some prefsLoad
            App.SavePrefs <- Some prefsSave
            do! AppBuilder.Configure<App>().StartBrowserAppAsync("out", options)
        }
        |> ignore

        0
