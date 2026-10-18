# UrlSchemeRegistrationIo（组合）

`wanxiang://` 协议注册的 IO 壳（决策 251），迁自旧 `src/Wanxiang.UI/Model/DesktopLinks.fs` 的 `UrlSchemeRegistration` 模块（旧仓库 bfdda85 删除；同一旧文件的 `ExternalLinks` 与 `SingleInstance` 模块分别在 `compose/ExternalLinks` 与 `libs/SingleInstance`）。纯文本部分（`desktopFileName` / `desktopEntry` / `quote`）在 `Wanxiang2.UrlSchemeRegistration` 库，本组合者经交付包引用它，只留与旧代码相同的 IO 面：

- `launchCommand ()`：apphost 直接用它；`dotnet wanxiang.dll` 形态带上程序集。
- `registerCurrentUser (log : string -> unit)`：Linux 写 `$XDG_DATA_HOME/applications/<desktopFileName>`，内容不变就不碰，然后调 `xdg-mime` / `update-desktop-database`；Windows 写 HKCU `Software\Classes\wanxiang`；macOS 不注册（`.app` 的 Info.plist 声明，`IActivatableLifetime` 收 `OpenUri`）。`WANXIANG_NO_URL_SCHEME=1` 关闭（开发 / 测试环境不改写用户的协议关联）。

命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

消费点：`app/Desktop/Program.fs` 在启动桌面客户端前调用 `Wanxiang.UI.UrlSchemeRegistration.registerCurrentUser logInfo`（与旧 `Program.fs` 同一位置）。

# LEMMA
1. **桌面文件字节与旧实现相同，且幂等。** [根 AXIOM: 1]
    - 推理：正文由 `Wanxiang2.UrlSchemeRegistration` 按旧字节生成（金样钉住）；内容不变就不写文件、不调 xdg 工具、不写注册表。
2. **尽力而为，失败只记日志。** [根 AXIOM: 5]
    - 推理：协议关联缺失只影响「点链接打开」，不该阻塞启动；旧代码就是吞异常交给 `log`。
3. **`launchCommand` 认 apphost 与 `dotnet <dll>` 两种启动形态。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] 纯文本金样与旧用例在 `libs/UrlSchemeRegistration/tests/Tests.fs`：`golden matches old code`（`tests/golden/expected.txt` 由旧程序集生成）与 `251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接`（旧用例原样迁入），通过；`./x test UrlSchemeRegistration` 可重跑。
2. 本组合者无独立测试工程：`./x compose` 跳过它（`x` 的 `compose()` 注释点名）。写桌面文件 / 注册表与 `launchCommand` 旧仓库也没有用例（旧仓库历史 `git log -S registerCurrentUser -- tests/` 无结果）；本壳保留旧代码逐字实现，写入动作由 `app/Desktop` 启动时执行。

# 迁入或覆盖的旧测试
- 旧 `PartialPersistAndUrlSchemeTests`：`251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接` → `libs/UrlSchemeRegistration/tests/Tests.fs`（`MimeType` / `Exec ... %u` / `NoDisplay=true` / `quote` 四条断言逐条相同）。
- `registerCurrentUser` / `registerLinux` / `registerWindows` / `launchCommand` 旧仓库没有用例可迁。

# 拆分与复核
- 旧模块一拆为二：`desktopFileName` / `desktopEntry` / `quote` 归 `libs/UrlSchemeRegistration`（旧程序集金样），`launchCommand` / `registerCurrentUser` 与 Linux / Windows 分支留本壳。桌面文件的字节由库的金样钉住，本壳不复制文本。
- 逐字对照：`git -C /tmp/wx-old show bfdda85^:src/Wanxiang.UI/Model/DesktopLinks.fs | sed -n '/^module UrlSchemeRegistration =/,/^\/\/\/ 决策 251：单实例转发/p' | head -n -1 | diff - compose/UrlSchemeRegistrationIo/UrlSchemeRegistrationIo.fs`（旧仓库退役检出在 `/tmp/wx-old`；`bfdda85^` 是这些文件被删除前的最后一版）。输出只有开头 9 行（命名空间 / `open` / 说明注释）与一个空行的差异。
