# 万象 · React 界面稿

这是要移植到 Avalonia 的那一版界面。本地演示数据，用来对照外观和交互，不是连主机的客户端。

## 跑起来

需要 Node 22。

```
npm install
npm run dev
```

浏览器打开 http://127.0.0.1:5173

## 给移植的人

界面只有三块，不要再叠卡片：

- 侧栏 `src/components/shell/sidebar.tsx`：品牌「万」+ 新对话、搜索、对话列表、页脚主机状态。
- 舞台 `stage.tsx` + `messages.tsx`：顶栏 52px；空态是「从一句开始」和三行起手式，跟底栏同一条中线。
- 底栏 `composer.tsx`：一条圆角输入。左附件、模型，右发送。占位「写下一句」。

尺寸和颜色全在 `src/styles.css` 的 `:root`：

- 侧栏宽 284，阅读列 748，空态列 480，栏高 52，图标按钮 30，圆角 16。
- 画布 `#f5f4ed`，栏 `#f0eee5`，强调色 `#1b365d`。
- 标题用衬线（Noto Serif SC），界面用黑体（Noto Sans SC）。

状态在 `src/lib/wanxiang/store.ts`，种子对话在 `seed.ts`。移植时照着这些文件的结构做，不要把旧 Avalonia 控件样式叠回去。
