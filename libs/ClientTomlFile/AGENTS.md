# ClientTomlFile

本机桌面客户端的 client.toml：读令牌原文、原子写 url 与令牌（决策 54；从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ClientTomlFile(path)`、`Path`、`TryReadToken()`、`Write(listen, token)`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **文件格式与读法与旧实现相同。唯一改动：临时文件先关闭再改名（旧实现在文件仍打开时改名，Windows 上会失败；Linux 行为不变）。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `写入后读回同一令牌，格式与旧实现相同`
  - `只有本人可读写`
  - `覆盖已有文件`
  - `不存在、空值、没有 token 行都读成 None`
  - `取第一条 token 行，容忍空白与无引号`
  - `目录不存在时写入返回错误而不抛`
  - `路径被目录占用时读成 None`
