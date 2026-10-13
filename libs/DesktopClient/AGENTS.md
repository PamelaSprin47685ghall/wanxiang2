# DesktopClient

「本机桌面客户端」这一身份：保留设备名 `wanxiang-desktop`，加上来自回环地址。纯函数，不依赖兄弟库（参数是名字、是否吊销、地址，不认识配置记录类型）。根规则见项目根目录 AGENTS.md。

公开面：`DesktopClient.Name`、`isReservedName`、`sanitizeName requested fallback`、`isLoopback`、`isDesktop name revoked address`。

# LEMMA
1. **保留名已写在用户配置里，不能改。** [根 AXIOM: 1]
2. **只有桌面客户端能发邀请；远端设备不能借用这个身份。** [根 AXIOM: 1, 6]
    - 推理：保留名只由本机自动配对写入。配对设备自报保留名时一律换掉；再加上回环限制，反向代理之外的远端即使拿到这个名字也冒充不了。

# SPEC
1. [LEMMA: 1, 2] 保留名不分大小写、忽略首尾空白，配对设备取不到它：`tests/Tests.fs` `reserved name cannot be taken by a paired device`，通过。
2. [LEMMA: 2] 桌面身份要同时满足保留名、未吊销、回环（含 IPv4 映射到 IPv6）：`desktop identity needs reserved name, not revoked, loopback`，通过。
