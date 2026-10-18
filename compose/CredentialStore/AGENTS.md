# CredentialStore（组合）

客户端凭据存储的 IO 壳（决策 52/53、Q191），迁自旧 `src/Wanxiang.UI/CredentialStore.fs`（旧仓库 bfdda85 删除）。公开面与旧代码相同：`clientName`、`tryLoadClientToml`（桌面读 `$WANXIANG_HOME/client.toml`）、`tryListBrowserConnectionsAsync` / `tryLoadBrowserConnectionAsync` / `saveBrowserConnectionAsync` / `deleteBrowserConnectionAsync`（浏览器 IndexedDB）、`defaultServerUrl`（页面同源 https → wss、http → ws；非浏览器回退 `desktopServerUrl`）。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

协作者（只经交付包引用）：纯解析 `parseClientToml` / `parseConnections` / `serverUrlOfPage` / `desktopServerUrl` 在 `Wanxiang2.ClientConnections`；浏览器 IndexedDB 与页面地址经 `Wanxiang2.BrowserCredentials` / `Wanxiang2.BrowserPage`（C# JSImport，绑定 `wwwroot/main.js`）。

消费点：`app/Desktop`（`tryLoadClientToml`、`tryLoadBrowserConnectionAsync`、`defaultServerUrl` 兜底）与 `app/Pwa`（浏览器连接读写、`defaultServerUrl`）。

# LEMMA
1. **桌面行为与旧代码一致。** [根 AXIOM: 1; ClientConnections LEMMA: 1]
    - 推理：用户磁盘上已有 client.toml，同一份文本要解析出同样的 url/token。浏览器分支不碰文件系统（wasm 无 File API），返回 `None`。
2. **浏览器分支是旧代码的逐字移植。** [根 AXIOM: 5]
    - 推理：旧 `Constants.WsPath` 在本文件内联为 `"/ws"`（值相同，见 `compose/ChatDomain` 的 `Constants.WsPath`），避免为一个常量引入领域包依赖。
3. **浏览器侧是外部契约：JS 函数名与 `wwwroot/main.js` 不变。** [BrowserCredentials LEMMA: 1; BrowserPage LEMMA: 1]

# SPEC
1. [LEMMA: 1] 解析部分：`libs/ClientConnections/tests` 的金标准用例（`tests/golden/expected.txt` 由旧程序集读真实临时 `WANXIANG_HOME` 下的 client.toml 生成）通过；`./x test ClientConnections` 可重跑。
2. [LEMMA: 3] JS 绑定名比对：`libs/BrowserCredentials/tests` 与 `libs/BrowserPage/tests` 通过；`./x test BrowserCredentials`、`./x test BrowserPage` 可重跑。
3. 本组合者无独立测试工程：`./x compose` 跳过它（`x` 的 `compose()` 注释点名）。旧仓库没有 CredentialStore 的单元测试可迁；浏览器分支的 JSImport 在浏览器之外不能实例化、更不能调（BrowserCredentials SPEC 1），桌面文件分支要真实文件系统。

# 迁入或覆盖的旧测试
无。旧仓库没有 CredentialStore 的单元测试（`libs/ClientConnections/AGENTS.md` 同结论）。新增的解析用例在 `libs/ClientConnections/tests/Tests.fs`：`Q191 浏览器连接记录：缺 url 或 token 的跳过，坏 JSON 当作没有`、`浏览器默认地址与页面同源：https 用 wss，http 用 ws`。

# 拆分与复核
- 旧文件按「纯解析 / 平台 IO」拆：`parseClientToml` / `parseConnections` / `serverUrlOfPage` / `desktopServerUrl` 归 `libs/ClientConnections`（金样 + 浏览器解析用例），`configHome`、桌面文件与 IndexedDB 调用、`clientName` 的平台判断留本壳。逐字对照：`git -C /tmp/wx-old show bfdda85^:src/Wanxiang.UI/CredentialStore.fs | diff - compose/CredentialStore/CredentialStore.fs`（旧仓库退役检出在 `/tmp/wx-old`），输出只有未用的 `open` 行、说明注释与末尾 `Constants.WsPath` → `"/ws"` 一处。
