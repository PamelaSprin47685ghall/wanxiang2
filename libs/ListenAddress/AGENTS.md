# ListenAddress

合并包：ListenAddress、WsHandshake。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ListenAddress`

## ListenAddress

监听地址是否只在本机回环上（从旧 ServerApp.WarnIfExposedWithoutTls 拆出）。公开面：`ListenAddress.hostOf`、`ListenAddress.isLoopback`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **判定与旧实现一致。** [根 AXIOM: 5]
2. **地址判定是纯规则；何时告警、写什么是宿主的事。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `回环判定与旧实现一致` 通过。

## 迁入或覆盖的旧测试
- 无单独旧测试（旧仓库只经 ServerApp 启动路径间接覆盖）。

## 成员 `WsHandshake`

## WsHandshake

浏览器 WebSocket 握手的来源检查：Origin 同源规则，以及 Host 白名单（防 DNS 重绑定）。纯函数，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`WsHandshake.isAllowedOrigin origin host`、`WsHandshake.isAllowedHost host extraAllowed`。

## LEMMA
1. **浏览器页面不能替别的网站连本机服务。** [根 AXIOM: 1]
    - 推理：有 Origin 时，它必须与 Host 同源。重绑定之后 Origin 和 Host 都是攻击者的域名，所以 Host 还得是 IP、localhost，或用户登记过的域名。
2. **原生客户端（没有 Origin）放行；令牌和配对码仍是唯一凭据。** [根 AXIOM: 6]

## SPEC
1. [LEMMA: 1, 2] 10 个 Origin 情形：`tests/Tests.fs` `origin must be same-origin http(s)`，通过。
2. [LEMMA: 1] 7 个 Host 情形（含 IPv6 方括号写法、登记名不分大小写）：`host must be an ip, localhost or listed`，通过。
