# FailureLimiter

合并包：FailureLimiter、PairingCode。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `FailureLimiter`

## FailureLimiter

按键计失败的对象：窗口内失败达到阈值就冻结一段时间。私有状态是每个键的失败时间和冻结期限。不依赖兄弟库，不知道键代表什么。根规则见项目根目录 AGENTS.md。

公开面：`FailureLimiter(window, maxFailures, freeze, ?pruneThreshold)`：`RecordFailure(now, key) : bool`（true 表示这次触发了冻结）、`IsFrozen`、`FrozenUntil`、`Clear`、`TrackedKeys`。

## LEMMA
1. **猜码要付出时间。** [根 AXIOM: 1]
2. **内存有界。** [根 AXIOM: 1]
    - 推理：攻击者可以换很多地址，每个只失败一次。条目数超过阈值时清理过期条目，单次成本 O(n)，摊下来可以忽略。
3. **时间由调用方给出。** [根 AXIOM: 2]
    - 推理：于是测试不必等真实时间，行为也完全确定。

## SPEC
1. [LEMMA: 1] 达到阈值冻结、键之间隔离、到期解除、`Clear` 立即解除：`tests/Tests.fs` `freezes at the threshold per key, expires, clears`，通过。
2. [LEMMA: 1] 窗口外的旧失败不累计：`old failures slide out of the window`，通过。
3. [LEMMA: 2] 1000 个不同键、阈值 100 时，跟踪的键数不超过 161：`many distinct keys stay bounded`，通过。
4. 非法参数拒绝构造：`max failures must be positive`，通过。
5. [LEMMA: 1] `Retune` 换限额不丢已记的失败与已有冻结：`retune keeps counted failures and existing freezes`，通过。

## 成员 `PairingCode`

## PairingCode

配对码对象：6 位十进制、密码学随机、单次使用、到期失效、同时只有一个有效。私有状态是当前会话，码只在内存里。不依赖兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`PairingCodes()`：`Start(now, lifetime)`、`StartInvite(now, lifetime)`（都返回 `PairingSession`）、`ActiveInvite now`、`HasInviteSession`、`TryConsume(now, code) : Result<unit, ConsumeError>`、`Session`。`ConsumeError = NoSession | AlreadyUsed | Expired | WrongCode`，各有 `Describe()`。

## LEMMA
1. **码短命、单次、唯一。** [根 AXIOM: 1]
    - 推理：6 位码的熵很低，只能靠短时、单次、同时只有一个，再加外部限流来保证安全。限流不是本对象的事（见 FailureLimiter），所以错误的码不消耗会话。
2. **错误类型化，文案沿用旧版。** [根 AXIOM: 5]
    - 推理：组合者要按错误种类处置；用户在界面上看到的文字不变。
3. **邀请码用过或过期后，仍让提交它的设备得到明确回执。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 完整生命周期：无会话、错码不消耗、新码顶替旧码、单次使用、过期后清除、null 码：`tests/Tests.fs` `full lifecycle`，通过。
2. [LEMMA: 3] 邀请仍有效时可查；用过之后 `HasInviteSession` 仍为真；普通 `Start` 清掉邀请：`invite is visible while valid and reported after use`，通过。
3. [LEMMA: 2] 文案与旧版一致：`errors describe themselves with the existing wording`，通过。
