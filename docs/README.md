# docs/

产品知识与历史档案，迁自旧仓库 wanxiang（2026-10-17 物理退役时整体迁入）。

- `contracts/`（4 篇）、`security/`（1 篇）、`ux/`（9 篇）、`adr/`（1 篇）：活文档，随产品演进继续维护。
- `evidence/`：各轮交付的文字与日志证据包（`2026-10-23-handoff/`、`2026-10-24-verified-release/`，共 386 个文件、约 1.7 MB）。注意仓库根 `.gitignore` 忽略一切名为 `artifacts/` 的目录，因此实跑过程中的临时构建物与截图输出（`artifacts/...`）不入 Git 索引；入库部分为 `MANIFEST.json` 与 `logs/` 原始日志，各 `artifacts/` 路径的本机再生成命令见对应目录下的 `README.md`。
- `archive/`：历史档案，只读，不再修改。
  - `handoffs/`：迁移交接记录与后续的历史账目（15 篇，含从根 `AGENTS.md` 迁入的《落地起点》《迁移状态》《抽取路线》《包合并》《收束后整理》《例外清单已解除项》）。
  - `site/`：旧品牌页（index.html 与 logo.png）。
  - `packaging/README.md`：决策 251 的 URL scheme 平台知识。
