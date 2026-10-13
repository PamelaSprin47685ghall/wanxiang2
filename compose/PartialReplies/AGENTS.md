# PartialReplies（组合）

把 Wire 的 `generation.partial` 事件和文件系统组合成旧 `Wanxiang.Server.PartialReplyStore`：每个对话最多保存一份半截回复，主机重启后仍在。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **文件内容就是事件的线上字节。** [根 AXIOM: 5]
    - 推理：读回来可以原样下发；格式由 Wire 的金样保证。
2. **坏文件跳过，写盘失败要说出来。** [根 AXIOM: 4]
    - 推理：一个坏文件不能拖垮启动；写盘静默失败会让「继续生成」在重启后悄悄消失。

# SPEC
1. [LEMMA: 1] `file holds exactly the wire bytes...`，通过。
2. [LEMMA: 2] `broken files and other events are skipped...`，通过。
3. 迁自旧仓库决策 250 的持久化测试，通过。
