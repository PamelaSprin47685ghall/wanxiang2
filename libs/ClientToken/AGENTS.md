# ClientToken

合并包：ClientToken、DesktopClient。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ClientToken`

## ClientToken

每台设备一个的永久令牌：生成、哈希、常量时间校验、设备标识。纯函数，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`ClientToken.Prefix`、`generate`、`hash`、`matches token storedHash`、`fixedTimeEquals`、`deviceId`。

## LEMMA
1. **令牌和哈希格式不能变。** [根 AXIOM: 1, 5]
    - 推理：令牌原文在用户的设备里，哈希在用户的配置文件里；格式一变，所有已配对设备都得重新配对。所以前缀、base64url 无填充、SHA-256(UTF-8) 小写十六进制都原样保留，设备标识（哈希前 16 位）也保留，因为设备管理界面拿它指代设备。
2. **配置里只存哈希，比较用常量时间。** [根 AXIOM: 1]
    - 推理：配置文件泄露时，攻击者拿不到能用的令牌；比较时间不随前缀匹配长度变化。

## SPEC
1. [LEMMA: 1] 前缀加 43 个 base64url 字符（256 bit），每次不同：`tests/ClientToken.Tests.fs` `tokens have the fixed prefix, base64url body and 256 bits`，通过。
2. [LEMMA: 1] 哈希等于用 Python hashlib 独立算出的金标准值：`hash matches the golden vector stored in existing configs`，通过。
3. [LEMMA: 2] `matches` 只认对应的哈希，null 不认：`matches accepts only the right token`，通过。
4. [LEMMA: 1] 设备标识是哈希前 16 位：`device id is the first 16 hex chars of the hash`，通过。

## 成员 `DesktopClient`

## DesktopClient

「本机桌面客户端」这一身份：保留设备名 `wanxiang-desktop`，加上来自回环地址。纯函数，不依赖兄弟库（参数是名字、是否吊销、地址，不认识配置记录类型）。根规则见项目根目录 AGENTS.md。

公开面：`DesktopClient.Name`、`isReservedName`、`sanitizeName requested fallback`、`isLoopback`、`isDesktop name revoked address`。

## LEMMA
1. **保留名已写在用户配置里，不能改。** [根 AXIOM: 1]
2. **只有桌面客户端能发邀请；远端设备不能借用这个身份。** [根 AXIOM: 1, 6]
    - 推理：保留名只由本机自动配对写入。配对设备自报保留名时一律换掉；再加上回环限制，反向代理之外的远端即使拿到这个名字也冒充不了。

## SPEC
1. [LEMMA: 1, 2] 保留名不分大小写、忽略首尾空白，配对设备取不到它：`tests/DesktopClient.Tests.fs` `reserved name cannot be taken by a paired device`，通过。
2. [LEMMA: 2] 桌面身份要同时满足保留名、未吊销、回环（含 IPv4 映射到 IPv6）：`desktop identity needs reserved name, not revoked, loopback`，通过。
