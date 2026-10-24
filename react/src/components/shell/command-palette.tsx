import { useEffect, useMemo, useState } from "react";
import { Dialog } from "@/components/shell/dialog";
import { useActive, useWx } from "@/lib/wanxiang/store";
import type { Conversation } from "@/lib/wanxiang/types";

function download(conv: Conversation) {
  const blob = new Blob([JSON.stringify(conv, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${conv.title.replace(/[^\w\u4e00-\u9fff-]+/g, "_").slice(0, 40) || "dialogue"}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

export function CommandPalette() {
  const open = useWx((s) => s.palette);
  const setPalette = useWx((s) => s.setPalette);
  const create = useWx((s) => s.newConversation);
  const openSettings = useWx((s) => s.openSettings);
  const setTheme = useWx((s) => s.setTheme);
  const theme = useWx((s) => s.theme);
  const toggleNav = useWx((s) => s.toggleNav);
  const active = useActive();
  const [q, setQ] = useState("");
  const [cursor, setCursor] = useState(0);

  useEffect(() => {
    if (open) {
      setQ("");
      setCursor(0);
    }
  }, [open]);

  const commands = useMemo(
    () =>
      [
        { id: "new", label: "新对话", run: create },
        { id: "settings", label: "打开设置", run: () => openSettings("models") },
        { id: "devices", label: "添加设备", run: () => openSettings("devices") },
        {
          id: "theme",
          label: theme === "dark" ? "切换到浅色" : "切换到深色",
          run: () => setTheme(theme === "dark" ? "light" : "dark"),
        },
        { id: "rail", label: "显示或收起侧栏", run: toggleNav },
        {
          id: "export",
          label: "导出当前对话",
          run: () => {
            if (active) download(active);
          },
          disabled: !active,
        },
      ].filter((c) => !c.disabled && c.label.toLowerCase().includes(q.trim().toLowerCase())),
    [active, create, openSettings, q, setTheme, theme, toggleNav],
  );

  useEffect(() => {
    setCursor(0);
  }, [q]);

  return (
    <Dialog open={open} title="命令" description="跳到常用动作。" onClose={() => setPalette(false)} variant="palette">
      <div className="dialog-body">
        <input
          className="palette-input"
          autoFocus
          placeholder="输入以筛选"
          aria-label="筛选命令"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "ArrowDown") {
              e.preventDefault();
              setCursor((i) => Math.min(commands.length - 1, i + 1));
            }
            if (e.key === "ArrowUp") {
              e.preventDefault();
              setCursor((i) => Math.max(0, i - 1));
            }
            if (e.key === "Enter" && commands[cursor]) {
              e.preventDefault();
              commands[cursor].run();
              setPalette(false);
            }
          }}
        />
        {commands.length === 0 ? <p className="hint">没有这条命令</p> : null}
        {commands.map((c, i) => (
          <button
            key={c.id}
            type="button"
            className="menu-item"
            data-active={i === cursor}
            onMouseEnter={() => setCursor(i)}
            onClick={() => {
              c.run();
              setPalette(false);
            }}
          >
            <span>{c.label}</span>
          </button>
        ))}
      </div>
    </Dialog>
  );
}
