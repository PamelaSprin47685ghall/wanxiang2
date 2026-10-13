# ClientToken

每台设备一个的永久令牌：生成、哈希、常量时间校验、设备标识。纯函数，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`ClientToken.Prefix`、`generate`、`hash`、`matches token storedHash`、`fixedTimeEquals`、`deviceId`。

# LEMMA
1. **令牌和哈希格式不能变。** [根 AXIOM: 1, 5]
    - 推理：令牌原文在用户的设备里，哈希在用户的配置文件里；格式一变，所有已配对设备都得重新配对。所以前缀、base64url 无填充、SHA-256(UTF-8) 小写十六进制都原样保留，设备标识（哈希前 16 位）也保留，因为设备管理界面拿它指代设备。
2. **配置里只存哈希，比较用常量时间。** [根 AXIOM: 1]
    - 推理：配置文件泄露时，攻击者拿不到能用的令牌；比较时间不随前缀匹配长度变化。

# SPEC
1. [LEMMA: 1] 前缀加 43 个 base64url 字符（256 bit），每次不同：`tests/Tests.fs` `tokens have the fixed prefix, base64url body and 256 bits`，通过。
2. [LEMMA: 1] 哈希等于用 Python hashlib 独立算出的金标准值：`hash matches the golden vector stored in existing configs`，通过。
3. [LEMMA: 2] `matches` 只认对应的哈希，null 不认：`matches accepts only the right token`，通过。
4. [LEMMA: 1] 设备标识是哈希前 16 位：`device id is the first 16 hex chars of the hash`，通过。
