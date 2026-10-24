import { format, isToday, isYesterday } from "date-fns";
import { zhCN } from "date-fns/locale";
import { Plus, Search, Settings } from "lucide-react";
import { useEffect, useRef } from "react";
import { modelById } from "@/lib/wanxiang/types";
import { useWx } from "@/lib/wanxiang/store";
import { COMPACT_MQ } from "@/lib/wanxiang/contract";
import { useMediaQuery } from "@/lib/wanxiang/project";

function stamp(ts: number) {
  const d = new Date(ts);
  if (isToday(d)) return format(d, "HH:mm", { locale: zhCN });
  if (isYesterday(d)) return "昨天";
  return format(d, "M月d日", { locale: zhCN });
}

function snippet(text: string, q: string) {
  const hay = text.replace(/\s+/g, " ");
  const i = hay.toLowerCase().indexOf(q.toLowerCase());
  if (i < 0) return null;
  const start = Math.max(0, i - 16);
  const end = Math.min(hay.length, i + q.length + 24);
  return `${start > 0 ? "…" : ""}${hay.slice(start, end)}${end < hay.length ? "…" : ""}`;
}

export function Sidebar() {
  const conversations = useWx((s) => s.conversations);
  const activeId = useWx((s) => s.activeId);
  const query = useWx((s) => s.query);
  const setQuery = useWx((s) => s.setQuery);
  const showArchive = useWx((s) => s.showArchive);
  const setShowArchive = useWx((s) => s.setShowArchive);
  const select = useWx((s) => s.selectConversation);
  const create = useWx((s) => s.newConversation);
  const openSettings = useWx((s) => s.openSettings);
  const providers = useWx((s) => s.providers);
  const drawer = useWx((s) => s.drawer);
  const searchRef = useRef<HTMLInputElement>(null);
  const ready = providers.some((p) => p.connected);
  const narrow = useMediaQuery(COMPACT_MQ);

  useEffect(() => {
    if (!drawer) return;
    const prev = document.activeElement as HTMLElement | null;
    searchRef.current?.focus();
    return () => prev?.focus?.();
  }, [drawer]);

  const q = query.trim();
  const archivedCount = conversations.filter((c) => c.archived).length;
  const pool = conversations.filter((c) => {
    if (q) return true;
    return showArchive ? c.archived : !c.archived;
  });
  const hits = pool
    .map((c) => {
      const blob = `${c.title}\n${c.messages.map((m) => m.text).join("\n")}`;
      if (q && !blob.toLowerCase().includes(q.toLowerCase())) return null;
      const snip = q
        ? (c.messages.map((m) => snippet(m.text, q)).find(Boolean) ?? null)
        : null;
      return { c, snip };
    })
    .filter((x): x is { c: (typeof conversations)[number]; snip: string | null } => x !== null)
    .sort((a, b) => b.c.updatedAt - a.c.updatedAt);

  const groups = q
    ? [{ label: "结果", items: hits }]
    : showArchive
      ? [{ label: "归档", items: hits }]
      : [
          { label: "置顶", items: hits.filter((h) => h.c.pinned) },
          { label: "今天", items: hits.filter((h) => !h.c.pinned && isToday(h.c.updatedAt)) },
          { label: "昨天", items: hits.filter((h) => !h.c.pinned && isYesterday(h.c.updatedAt)) },
          {
            label: "更早",
            items: hits.filter((h) => !h.c.pinned && !isToday(h.c.updatedAt) && !isYesterday(h.c.updatedAt)),
          },
        ].filter((g) => g.items.length > 0);

  return (
    <aside className="rail" aria-label="对话" inert={narrow && !drawer ? true : undefined}>
      <div className="rail-head">
        <div className="brand">
          <span className="mark" aria-hidden>
            万
          </span>
          <div className="wordmark">万象</div>
        </div>
        <button className="icon-btn" type="button" aria-label="新对话" onClick={create}>
          <Plus className="icon" aria-hidden />
        </button>
      </div>
      <label className="search">
        <Search className="icon-sm" aria-hidden />
        <input
          ref={searchRef}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="搜索标题和正文"
          aria-label="搜索标题和正文"
        />
      </label>
      <div className="rail-list">
        {groups.length === 0 ? <p className="empty-note">{q ? "没有匹配的对话" : "还没有对话"}</p> : null}
        {groups.map((g) => (
          <section key={g.label}>
            <h2 className="section-label">{g.label}</h2>
            {g.items.map(({ c, snip }) => (
              <button
                key={c.id}
                type="button"
                className="row"
                aria-current={c.id === activeId ? "page" : undefined}
                onClick={() => select(c.id)}
              >
                <span className="row-title">{c.title}</span>
                <span className="row-meta" suppressHydrationWarning>
                  {c.forkedFrom ? "分叉 · " : ""}
                  {c.archived ? "归档 · " : ""}
                  {modelById(c.modelId).label} · {stamp(c.updatedAt)}
                </span>
                {snip ? <span className="row-snip">{snip}</span> : null}
              </button>
            ))}
          </section>
        ))}
      </div>
      {archivedCount > 0 ? (
        <div className="rail-tools">
          <button className="text-btn" type="button" onClick={() => setShowArchive(!showArchive)}>
            {showArchive ? "返回对话" : `归档 ${archivedCount}`}
          </button>
        </div>
      ) : null}
      <footer className="rail-foot">
        <span className="dot" data-off={ready ? undefined : "true"} />
        <div className="foot-copy">
          <strong>演示主机</strong>
          <span>{ready ? "已连接 · 本地回复，不出网" : "未连接模型"}</span>
        </div>
        <button className="icon-btn" type="button" aria-label="设置" onClick={() => openSettings("models")}>
          <Settings className="icon" aria-hidden />
        </button>
      </footer>
    </aside>
  );
}
