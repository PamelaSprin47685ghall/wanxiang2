# FailureLimiter

按键计失败的对象：窗口内失败达到阈值就冻结一段时间。私有状态是每个键的失败时间和冻结期限。不依赖兄弟库，不知道键代表什么。根规则见项目根目录 AGENTS.md。

公开面：`FailureLimiter(window, maxFailures, freeze, ?pruneThreshold)`：`RecordFailure(now, key) : bool`（true 表示这次触发了冻结）、`IsFrozen`、`FrozenUntil`、`Clear`、`TrackedKeys`。

# LEMMA
1. **猜码要付出时间。** [根 AXIOM: 1]
2. **内存有界。** [根 AXIOM: 1]
    - 推理：攻击者可以换很多地址，每个只失败一次。条目数超过阈值时清理过期条目，单次成本 O(n)，摊下来可以忽略。
3. **时间由调用方给出。** [根 AXIOM: 2]
    - 推理：于是测试不必等真实时间，行为也完全确定。

# SPEC
1. [LEMMA: 1] 达到阈值冻结、键之间隔离、到期解除、`Clear` 立即解除：`tests/Tests.fs` `freezes at the threshold per key, expires, clears`，通过。
2. [LEMMA: 1] 窗口外的旧失败不累计：`old failures slide out of the window`，通过。
3. [LEMMA: 2] 1000 个不同键、阈值 100 时，跟踪的键数不超过 161：`many distinct keys stay bounded`，通过。
4. 非法参数拒绝构造：`max failures must be positive`，通过。
5. [LEMMA: 1] `Retune` 换限额不丢已记的失败与已有冻结：`retune keeps counted failures and existing freezes`，通过。
