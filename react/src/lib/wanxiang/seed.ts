import type { Conversation, Provider } from "@/lib/wanxiang/types";

function hoursAgo(hours: number) {
  const d = new Date();
  d.setMinutes(0, 0, 0);
  d.setHours(d.getHours() - hours);
  return d.getTime();
}

function daysAgo(days: number, hour: number) {
  const d = new Date();
  d.setHours(hour, 0, 0, 0);
  d.setDate(d.getDate() - days);
  return d.getTime();
}

function msg(
  id: string,
  role: "user" | "assistant",
  text: string,
  at: number,
  extra?: Partial<Conversation["messages"][number]>,
): Conversation["messages"][number] {
  return { id, role, text, createdAt: at, ...extra };
}

const alignAt = daysAgo(2, 18);
const toolsAt = hoursAgo(1);
const errorAt = hoursAgo(3);
const pairAt = daysAgo(1, 16);
const forkAt = daysAgo(4, 11);
const oldAt = daysAgo(12, 9);

export const seedProviders: Provider[] = [
  {
    id: "prov-ollama",
    kind: "ollama",
    name: "本机演示",
    connected: true,
    keyHint: null,
  },
];

export const seedConversations: Conversation[] = [
  {
    id: "c-align",
    title: "纸面与墨色：不要再靠绝对定位对齐",
    pinned: true,
    archived: false,
    createdAt: alignAt,
    updatedAt: alignAt + 60_000,
    modelId: "qwen-local",
    system: "",
    forkedFrom: null,
    messages: [
      msg(
        "m-align-1",
        "user",
        "侧栏标题和代码块把整页撑出横向滚动。先别改功能，只把布局契约说清楚。另有一条不该撑开栏宽的地址：https://example.com/wanxiang/conversations/very-long-identifier-that-must-wrap-inside-the-column",
        alignAt,
      ),
      msg(
        "m-align-2",
        "assistant",
        "撑破页面的通常不是缺了一点内边距。父级一旦是 flex 或 grid，子项最小宽度默认等于内容宽度。\n\n```css\n.shell {\n  display: grid;\n  grid-template-columns: 284px minmax(0, 1fr);\n  height: 100dvh;\n  overflow: hidden;\n}\n.stage, .thread, .column, .msg { min-width: 0; }\n.prose { overflow-wrap: anywhere; }\npre { max-width: 100%; overflow-x: auto; }\n```\n\n长地址留在正文里换行。代码只在深色块里横向滚动，不带动整页。",
        alignAt + 20_000,
      ),
      msg("m-align-3", "user", "圆角和间距呢？现在每个控件各写各的。", alignAt + 40_000),
      msg(
        "m-align-4",
        "assistant",
        "间距只走刻度：4、8、12、16、20、24、32。需要 2、6、10 时也只用于图标内衬，不拿来凑页面留白。\n\n圆角按嵌套算。输入壳圆角 8，外套内边距 8，外套圆角就是 16。父子写同一个圆角，曲线会互相打架。",
        alignAt + 60_000,
      ),
    ],
  },
  {
    id: "c-tools",
    title: "工具调用应该说人话",
    pinned: false,
    archived: false,
    createdAt: toolsAt,
    updatedAt: toolsAt + 30_000,
    modelId: "qwen-local",
    system: "回答先给结论，再给依据。",
    forkedFrom: null,
    messages: [
      msg("m-tools-1", "user", "工具调用别再把 JSON 铺满对话。读取文件就说读了哪个文件。", toolsAt),
      msg(
        "m-tools-2",
        "assistant",
        "读完了。文件里有三件未完成的事，没有碰到授权目录以外的路径。\n\n之后每一条调用都是这一行摘要，参数按需展开。",
        toolsAt + 30_000,
        {
          tool: {
            summary: "读取了 notes/todo.md",
            detail:
              "path: /home/me/notes/todo.md\nbytes: 842\nroots: 未越出已授权的文件夹",
          },
        },
      ),
    ],
  },
  {
    id: "c-error",
    title: "生成中断后的显式重试",
    pinned: false,
    archived: false,
    createdAt: errorAt,
    updatedAt: errorAt + 10_000,
    modelId: "qwen-local",
    system: "",
    forkedFrom: null,
    messages: [
      msg("m-error-1", "user", "把布局契约写成可以执行的检查项。", errorAt),
      msg("m-error-2", "assistant", "", errorAt + 10_000, {
        error: "主机没有返回完整结果。没有自动重发。",
      }),
    ],
  },
  {
    id: "c-pair",
    title: "配对码不该只出现在终端里",
    pinned: false,
    archived: false,
    createdAt: pairAt,
    updatedAt: pairAt + 20_000,
    modelId: "qwen-local",
    system: "",
    forkedFrom: null,
    messages: [
      msg("m-pair-1", "user", "手机怎么连上这台主机？我不想去翻 stderr。", pairAt),
      msg(
        "m-pair-2",
        "assistant",
        "桌面已经登录时，由它发出 6 位码，旁边放地址和 5 分钟倒计时。手机打开页面，输入框停在这 6 位上。\n\n没有桌面客户端，才退回主机上的配对命令。码过期就作废，不自动续上。",
        pairAt + 20_000,
      ),
    ],
  },
  {
    id: "c-fork",
    title: "分叉：只改间距，不动协议",
    pinned: false,
    archived: false,
    createdAt: forkAt,
    updatedAt: forkAt + 40_000,
    modelId: "qwen-local",
    system: "",
    forkedFrom: { id: "c-align", title: "纸面与墨色：不要再靠绝对定位对齐", at: 2 },
    messages: [
      msg(
        "m-fork-1",
        "user",
        "侧栏标题和代码块把整页撑出横向滚动。先别改功能，只把布局契约说清楚。",
        forkAt,
      ),
      msg(
        "m-fork-2",
        "assistant",
        "父级用网格，列宽用 minmax(0, 1fr)。正文允许任意位置断行，代码块自己滚动。",
        forkAt + 15_000,
      ),
      msg("m-fork-3", "user", "只改间距刻度，协议一行都不要动。", forkAt + 25_000),
      msg(
        "m-fork-4",
        "assistant",
        "协议、事件日志和生成语义都不碰。这一支只改用户看得见的间距和圆角，旧客户端仍能忽略不认识的界面。",
        forkAt + 40_000,
        {
          versions: [
            "这一支只谈间距。",
            "协议、事件日志和生成语义都不碰。这一支只改用户看得见的间距和圆角，旧客户端仍能忽略不认识的界面。",
          ],
          versionIndex: 1,
        },
      ),
    ],
  },
  {
    id: "c-old",
    title: "旧稿：品牌卡空态（已弃用）",
    pinned: false,
    archived: true,
    createdAt: oldAt,
    updatedAt: oldAt + 10_000,
    modelId: "qwen-local",
    system: "",
    forkedFrom: null,
    messages: [
      msg("m-old-1", "user", "空态再放一张品牌卡？", oldAt),
      msg(
        "m-old-2",
        "assistant",
        "不放。没有消息时只留起步问题和一句说明，新建入口只留在侧栏。",
        oldAt + 10_000,
      ),
    ],
  },
];
