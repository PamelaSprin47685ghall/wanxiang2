# HostProbe

主机探测：端口是否被占（`portInUse`）、目录所在盘剩余字节（`freeBytes`，取不到时为 None）、目录是否对组或其他人可访问（`groupOrOtherAccessible`）。只问操作系统，不做判断。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **只回答事实，阈值和措辞归调用方。** [根 AXIOM: 3]
    - 推理：doctor 的判断文字是组合者的格式，探测本身与业务无关。

# SPEC
1. [LEMMA: 1] 测试覆盖占用端口、存在目录的剩余空间、0700 与 0755 权限，通过。
