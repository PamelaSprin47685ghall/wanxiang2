# LineProcess

按行通信的子进程：写一行进 stdin，stdout 每行回调一次，stderr 单独回调，退出只报告一次。不懂 JSON。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`LineProcess(command, args, env, onLine, onStderr, onExit)`、`Environment`、`IsRunning`、`Start`、`WriteLine`、`Kill`。

# LEMMA
1. **子进程只继承白名单环境变量。** [根 AXIOM: 4]
    - 推理：第三方 MCP 服务器不该读到宿主的密钥。白名单是 PATH、HOME、LANG、LC_ALL、TMPDIR、TZ，再加上调用方显式给的变量。
2. **Kill 杀整个进程树。** [根 AXIOM: 4]
    - 推理：npx 一类启动器会再起孙进程。只杀父进程会留下孤儿。

# SPEC
1. [LEMMA: 1] `child sees only the whitelist...`，通过。
2. [LEMMA: 2] `stderr goes to its own sink; kill ends...`，通过。
3. 行进行出、退出只报一次：`lines go in and come out...`，通过。
