# WsHandshake

浏览器 WebSocket 握手的来源检查：Origin 同源规则，以及 Host 白名单（防 DNS 重绑定）。纯函数，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`WsHandshake.isAllowedOrigin origin host`、`WsHandshake.isAllowedHost host extraAllowed`。

# LEMMA
1. **浏览器页面不能替别的网站连本机服务。** [根 AXIOM: 1]
    - 推理：有 Origin 时，它必须与 Host 同源。重绑定之后 Origin 和 Host 都是攻击者的域名，所以 Host 还得是 IP、localhost，或用户登记过的域名。
2. **原生客户端（没有 Origin）放行；令牌和配对码仍是唯一凭据。** [根 AXIOM: 6]

# SPEC
1. [LEMMA: 1, 2] 10 个 Origin 情形：`tests/Tests.fs` `origin must be same-origin http(s)`，通过。
2. [LEMMA: 1] 7 个 Host 情形（含 IPv6 方括号写法、登记名不分大小写）：`host must be an ip, localhost or listed`，通过。
