# PairingCode

配对码对象：6 位十进制、密码学随机、单次使用、到期失效、同时只有一个有效。私有状态是当前会话，码只在内存里。不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`PairingCodes()`：`Start(now, lifetime)`、`StartInvite(now, lifetime)`（都返回 `PairingSession`）、`ActiveInvite now`、`HasInviteSession`、`TryConsume(now, code) : Result<unit, ConsumeError>`、`Session`。`ConsumeError = NoSession | AlreadyUsed | Expired | WrongCode`，各有 `Describe()`。

# LEMMA
1. **码短命、单次、唯一。** [根 AXIOM: 1]
    - 推理：6 位码的熵很低，只能靠短时、单次、同时只有一个，再加外部限流来保证安全。限流不是本对象的事（见 FailureLimiter），所以错误的码不消耗会话。
2. **错误类型化，文案沿用旧版。** [根 AXIOM: 5]
    - 推理：组合者要按错误种类处置；用户在界面上看到的文字不变。
3. **邀请码用过或过期后，仍让提交它的设备得到明确回执。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 完整生命周期：无会话、错码不消耗、新码顶替旧码、单次使用、过期后清除、null 码：`tests/Tests.fs` `full lifecycle`，通过。
2. [LEMMA: 3] 邀请仍有效时可查；用过之后 `HasInviteSession` 仍为真；普通 `Start` 清掉邀请：`invite is visible while valid and reported after use`，通过。
3. [LEMMA: 2] 文案与旧版一致：`errors describe themselves with the existing wording`，通过。
