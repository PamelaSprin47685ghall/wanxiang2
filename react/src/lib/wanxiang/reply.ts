import type { ToolCard } from "@/lib/wanxiang/types";

export function draftReply(userText: string): { text: string; tool?: ToolCard } {
  const q = userText.trim();
  if (/工具|人话|读取|文件/.test(q)) {
    return {
      tool: {
        summary: "读取了 notes/todo.md",
        detail:
          "path: /home/me/notes/todo.md\nbytes: 842\nroots: 未越出已授权的文件夹\nitems:\n  - 把间距收进刻度\n  - 代码块单独滚动\n  - 配对码不要只打在终端",
      },
      text: "读完了。文件里有三件未完成的事，没有碰到授权目录以外的路径。\n\n调用先写成一句人话。原始参数收在这一行的展开里，不再把 JSON 铺满对话。",
    };
  }
  if (/toml|配置|错误|服务商|模型/.test(q)) {
    return {
      text: "有两处会让人误以为已经生效。\n\n1. 密钥填了，模型列表还没探测成功。\n2. 默认模型写在某一家服务下面，新对话看不出「全局默认」。\n\n可以写成：\n\n```toml\n[generation]\ndefaultProvider = \"anthropic\"\ndefaultModel = \"claude-sonnet\"\n```\n\n保存仍走同一条原子重写。探测失败要写明是密钥无效、网络不通，还是端点路径错，不能只说「失败」。",
    };
  }
  if (/配对|手机|设备/.test(q)) {
    return {
      text: "手机上只留一条路：已经打开的桌面客户端发出 6 位码，旁边是地址和 5 分钟倒计时。\n\n不要再让人去终端里翻一行 stderr。没有桌面客户端时，才退回在运行万象的机器上执行配对命令。",
    };
  }
  if (/溢出|对齐|布局|间距|圆角|前端|画布/.test(q)) {
    return {
      text: "撑破页面的通常不是缺了一点内边距，而是父级一旦变成 flex 或 grid，子项最小宽度默认等于内容宽度。长链接、代码行、不换行的标题会把整列顶出视口。\n\n契约就这几条，写进样式，不写进每个页面：\n\n```css\n.shell {\n  display: grid;\n  grid-template-columns: 284px minmax(0, 1fr);\n  height: 100dvh;\n  overflow: hidden;\n}\n.stage, .thread, .column, .msg { min-width: 0; }\n.prose { overflow-wrap: anywhere; }\npre { max-width: 100%; overflow-x: auto; }\n```\n\n间距只走刻度。外层圆角等于内层圆角加上内边距，嵌套才贴得住。控件用同一套类，不在页面里临时写像素。",
    };
  }
  const quote = q.replace(/\s+/g, " ").slice(0, 80);
  return {
    text: `按你刚才的说法，先把范围收住。\n\n「${quote}」\n\n这台预览在浏览器里排版，回复也停在本地，用来核对栏宽：句子在栏内换行，代码只在自己的块里横向滚动，侧栏标题超出就省略。要接真实模型，在设置里连接一家模型服务。`,
  };
}
