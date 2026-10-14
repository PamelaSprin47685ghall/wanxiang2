# ClientConnections

客户端连接凭据的纯解析（决策 52/53/64、Q191），迁自旧 `src/Wanxiang.UI/CredentialStore.fs`：`CredentialStore.parseClientToml`（client.toml 的 url/token）、`parseConnections`（IndexedDB 连接记录 JSON → (url, token, instanceId)）、`serverUrlOfPage wsPath href`（与页面同源的 ws/wss 地址）、`desktopServerUrl`。读文件、IndexedDB（BrowserCredentials）、页面地址、`clientName` 的平台判断归宿主；宿主的 `tryLoadClientToml` / `tryListBrowserConnectionsAsync` / `defaultServerUrl` 取到文本后调本库，`wsPath` 传 ChatDomain 的 `Constants.WsPath`。与桌面 App 的 ClientTomlFile（只读 token 行、原子写）是两份不同的旧规则，各自保留原行为。只依赖 BCL；命名空间沿用 `Wanxiang.UI`，模块与宿主同名合并。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **读取结果与旧代码一致。** [根 AXIOM: 1] 同一份 client.toml 解析出的连接与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧程序集真实读临时 WANXIANG_HOME 下的 client.toml 生成；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`；重跑后 `tests/golden` 逐字节不变。
3. 浏览器专用的 `parseConnections`、`serverUrlOfPage` 是旧代码逐字移植，旧代码在桌面跑不到，由新用例覆盖。

# 迁入或覆盖的旧测试
- 旧仓库没有 CredentialStore 的单元测试；新增用例：`Q191 浏览器连接记录：缺 url 或 token 的跳过，坏 JSON 当作没有`、`浏览器默认地址与页面同源：https 用 wss，http 用 ws`。
