import * as Menu from "@radix-ui/react-dropdown-menu";
import { Archive, Download, GitFork, MoreHorizontal, PanelLeft, Pencil, Pin, Trash2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Composer } from "@/components/shell/composer";
import { Dialog } from "@/components/shell/dialog";
import { MessageList } from "@/components/shell/messages";
import { useActive, useWx } from "@/lib/wanxiang/store";
import { useShellLayout } from "@/lib/wanxiang/project";
import type { Conversation } from "@/lib/wanxiang/types";

const PROMPTS = [
  "用三句话说明这台主机现在能做什么",
  "指出一份 TOML 配置里的错误",
  "把工具调用说成人话",
];

function download(conv: Conversation) {
  const blob = new Blob([JSON.stringify(conv, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${conv.title.replace(/[^\w\u4e00-\u9fff-]+/g, "_").slice(0, 40) || "dialogue"}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

export function Stage() {
  const active = useActive();
  const streaming = useWx((s) => s.streaming);
  const toggleNav = useWx((s) => s.toggleNav);
  const railOpen = useWx((s) => s.railOpen);
  const drawer = useWx((s) => s.drawer);
  const rename = useWx((s) => s.rename);
  const togglePin = useWx((s) => s.togglePin);
  const toggleArchive = useWx((s) => s.toggleArchive);
  const remove = useWx((s) => s.remove);
  const forkAt = useWx((s) => s.forkAt);
  const select = useWx((s) => s.selectConversation);
  const prefill = useWx((s) => s.prefill);
  const focusMessageId = useWx((s) => s.focusMessageId);
  const clearFocus = useWx((s) => s.clearFocus);
  const [renaming, setRenaming] = useState(false);
  const [title, setTitle] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [away, setAway] = useState(false);
  const titleRef = useRef<HTMLInputElement>(null);
  const scroller = useRef<HTMLDivElement>(null);
  const stick = useRef(true);
  const cancelRename = useRef(false);
  const messages = active?.messages ?? [];
  const layout = useShellLayout(messages.length, streaming);

  useEffect(() => {
    setRenaming(false);
  }, [active?.id]);
  const signature = messages.map((m) => `${m.id}:${m.text.length}:${m.error ?? ""}`).join("|");
  const empty = messages.length === 0;
  const center = layout.placement === "start";

  useEffect(() => {
    const el = scroller.current;
    if (!el || center || !stick.current) return;
    el.scrollTop = el.scrollHeight;
  }, [signature, center, streaming]);

  useEffect(() => {
    if (!focusMessageId) return;
    const el = document.getElementById(`msg-${focusMessageId}`);
    if (el) {
      stick.current = false;
      setAway(true);
      el.scrollIntoView({ block: "center" });
      el.classList.add("flash");
    }
    clearFocus();
  }, [focusMessageId, clearFocus]);

  useEffect(() => {
    if (renaming) titleRef.current?.focus();
  }, [renaming]);

  function onScroll() {
    const el = scroller.current;
    if (!el) return;
    const near = el.scrollHeight - el.scrollTop - el.clientHeight < 48;
    stick.current = near;
    setAway(!near);
  }

  function commitTitle() {
    if (cancelRename.current) {
      cancelRename.current = false;
      return;
    }
    rename(title);
    setRenaming(false);
  }

  const parent = active?.forkedFrom;
  const crumb = streaming || parent;

  return (
    <main className="stage">
      <header className="topbar">
        <button
          className="icon-btn"
          type="button"
          aria-label="对话列表"
          aria-expanded={layout.compact ? drawer : railOpen}
          onClick={toggleNav}
        >
          <PanelLeft className="icon" aria-hidden />
        </button>
        <div className="title-block">
          {renaming ? (
            <input
              ref={titleRef}
              className="title-input"
              value={title}
              aria-label="对话标题"
              onChange={(e) => setTitle(e.target.value)}
              onBlur={commitTitle}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  (e.target as HTMLInputElement).blur();
                }
                if (e.key === "Escape") {
                  cancelRename.current = true;
                  setRenaming(false);
                }
              }}
            />
          ) : (
            <h1 className="title">{active?.title ?? "新对话"}</h1>
          )}
          {crumb ? (
            <div className="crumb" role="status">
              {streaming ? <span className="static">正在回复</span> : null}
              {parent ? (
                <span className="crumb-fork">
                  <span className="static">来自</span>
                  <button className="link" type="button" onClick={() => select(parent.id)}>
                    「{parent.title}」
                  </button>
                  <span className="static">的第 {parent.at} 条</span>
                </span>
              ) : null}
            </div>
          ) : null}
        </div>
        {active ? (
          <Menu.Root>
            <Menu.Trigger className="icon-btn" aria-label="对话菜单">
              <MoreHorizontal className="icon" aria-hidden />
            </Menu.Trigger>
            <Menu.Portal>
              <Menu.Content className="popover" align="end" sideOffset={6}>
                <Menu.Item
                  className="menu-item"
                  onSelect={() => {
                    setTitle(active.title);
                    setRenaming(true);
                  }}
                >
                  <Pencil className="icon-sm" aria-hidden />
                  <span>重命名</span>
                </Menu.Item>
                <Menu.Item
                  className="menu-item"
                  disabled={!messages.length}
                  onSelect={() => {
                    const last = messages[messages.length - 1];
                    if (last) forkAt(last.id);
                  }}
                >
                  <GitFork className="icon-sm" aria-hidden />
                  <span>从最后一条分叉</span>
                </Menu.Item>
                <Menu.Item className="menu-item" onSelect={() => download(active)}>
                  <Download className="icon-sm" aria-hidden />
                  <span>导出</span>
                </Menu.Item>
                <Menu.Separator className="menu-sep" />
                <Menu.Item className="menu-item" onSelect={togglePin}>
                  <Pin className="icon-sm" aria-hidden />
                  <span>{active.pinned ? "取消置顶" : "置顶"}</span>
                </Menu.Item>
                <Menu.Item className="menu-item" onSelect={toggleArchive}>
                  <Archive className="icon-sm" aria-hidden />
                  <span>{active.archived ? "取消归档" : "归档"}</span>
                </Menu.Item>
                <Menu.Item className="menu-item" onSelect={() => setConfirmDelete(true)}>
                  <Trash2 className="icon-sm" aria-hidden />
                  <span>删除</span>
                </Menu.Item>
              </Menu.Content>
            </Menu.Portal>
          </Menu.Root>
        ) : (
          <span className="icon-btn" aria-hidden />
        )}
      </header>
      <div className="stage-body" data-compose={layout.placement}>
        <div className="thread" ref={center ? undefined : scroller} onScroll={center ? undefined : onScroll}>
          {empty ? (
            <div className="starter">
              <p className="lead">从一句开始</p>
              <p className="sub">模型、附件和发送都在下面。要找回以前的对话，用侧栏。</p>
              <div className="prompts">
                {PROMPTS.map((prompt) => (
                  <button key={prompt} className="prompt" type="button" onClick={() => prefill(prompt)}>
                    {prompt}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <>
              <MessageList messages={messages} streaming={streaming} />
              {away ? (
                <div className="jump-slot">
                  <button
                    className="btn"
                    type="button"
                    onClick={() => {
                      stick.current = true;
                      setAway(false);
                      const el = scroller.current;
                      if (el) el.scrollTop = el.scrollHeight;
                    }}
                  >
                    回到最新
                  </button>
                </div>
              ) : null}
            </>
          )}
        </div>
        <Composer />
      </div>
      <Dialog
        open={confirmDelete}
        title="删除这则对话？"
        description="删除后无法恢复。此预览只存在这台浏览器里。"
        onClose={() => setConfirmDelete(false)}
        narrow
      >
        <div className="dialog-body">
          <p className="hint">删除后无法恢复。此预览只存在这台浏览器里。</p>
          <div className="actions">
            <button
              className="btn btn-danger"
              type="button"
              onClick={() => {
                if (active) remove(active.id);
                setConfirmDelete(false);
              }}
            >
              删除
            </button>
            <button className="btn btn-ghost" type="button" onClick={() => setConfirmDelete(false)}>
              取消
            </button>
          </div>
        </div>
      </Dialog>
    </main>
  );
}
