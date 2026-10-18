# compose/PairingDesk

组合者 C：配对柜台。用 `PairingCodes`、`FailureLimiter`、`ClientToken`、`DesktopClient` 组成「设备凭码换令牌」这一段合作。只经交付包使用这四个库，本目录打成包 `Wanxiang2.PairingDesk`。根规则见项目根目录 AGENTS.md。

公开面：`PairingDesk(limits, codeLifetime)`：`Request(now, address) : RequestOutcome`、`Invite(now, deviceName, revoked, address, lifetime)`、`ActiveInvite now`、`Attempt(now, address, code, requestedName, requested) : AttemptOutcome`、`RecordFailure(now, address)`、`Paired address`、`Reconfigure limits`。

# LEMMA
1. **C 拥有顺序。** [根 LEMMA: 4]
    - 推理：顺序是查冻结 → 用码 → 失败计数或签发令牌。码、计数、令牌格式、桌面身份各归各的对象。持久化令牌哈希和发消息归宿主，因为宿主拥有配置和连接。「这条连接请求过配对」是连接自己的状态，由宿主作为参数传进来。
2. **先持久化，再交出令牌。** [根 AXIOM: 1]
    - 推理：`Accepted` 带着令牌原文和哈希；宿主先把哈希写进配置，成功后调用 `Paired`，再把原文发给设备（旧版决策 56）。写失败时设备拿不到一个服务端不认的令牌。
3. **桌面客户端仍有效的邀请不被覆盖。** [根 AXIOM: 5]
    - 推理：别的设备请求配对时沿用邀请码，不生成新码，否则扫码的人手里的码会失效。

# SPEC
（证明在 `tests/Tests.fs`，用真实的四个库，`./x compose` 运行，通过。）
1. [LEMMA: 1, 2] 请求、用码、签发的令牌能通过哈希校验；码只能用一次：`request, attempt, issue a token whose hash verifies; code is single use`。
2. [LEMMA: 1] 错码累计到阈值就冻结这个地址，冻结期间请求和用码都被拒，码不被消耗，别的地址不受影响：`wrong codes freeze the address; ...`。
3. [LEMMA: 2] `Paired` 清掉这个地址的失败记录：`paired clears failures for the address`。
4. [LEMMA: 3] 只有本机桌面客户端能发邀请；有效邀请被沿用；扫码的设备不先请求也能直接用邀请码，且取不到保留名：`only the local desktop client can invite; ...`。
5. 没请求也没邀请时，提交的码被忽略：`attempt without request or invite is ignored`。
6. `Reconfigure` 让新的限额生效：`reconfigure applies new limits`。
9. [LEMMA: 1] 配置重载（宿主每次重载都调 `Reconfigure`）不清空已记的失败与冻结：`a config reload between wrong codes does not erase the failures already counted`、`a reload that changes limits keeps an existing freeze and the counted failures`。这是旧仓库 DevicesTests「five failures」偶发失败的根因。
7. [LEMMA: 1] 与码无关的失败（未认证的连接要求发邀请）通过 `RecordFailure` 计入同一个限额，与旧 WsConnection 一致：`unauthenticated invites count toward the same limit as wrong codes`。
8. 「持久化失败时不发令牌」的证据已在本仓库：`compose/ServerApp/tests/DevicesTests.fs` 的 `config write failure during pairing sends no token`（配置目录只读这一真实故障：远端收到 `pairing.failed`，配置文本逐字不变、没写进新设备）；本机桌面一条在 `compose/DesktopPairing/tests/Tests.fs` 的 `配置无效时返回错误且不写 client.toml`。宿主侧接线见 `compose/WsConnection/WsConnection.fs:455`（先持久化哈希、成功后 `Paired`、再下发令牌）。
