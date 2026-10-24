import * as Popover from "@radix-ui/react-popover";
import { ArrowUp, Check, ChevronDown, Paperclip, Square, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Dialog } from "@/components/shell/dialog";
import { Onboarding } from "@/components/shell/onboarding";
import { MODELS, modelById } from "@/lib/wanxiang/types";
import { isReadyModel, useActive, useWx } from "@/lib/wanxiang/store";

export function Composer() {
  const active = useActive();
  const providers = useWx((s) => s.providers);
  const pendingModelId = useWx((s) => s.pendingModelId);
  const drafts = useWx((s) => s.drafts);
  const attachments = useWx((s) => s.attachments);
  const setDraft = useWx((s) => s.setDraft);
  const addAttachment = useWx((s) => s.addAttachment);
  const removeAttachment = useWx((s) => s.removeAttachment);
  const setModel = useWx((s) => s.setModel);
  const send = useWx((s) => s.send);
  const stop = useWx((s) => s.stop);
  const streaming = useWx((s) => s.streaming);
  const queued = useWx((s) => s.queued);
  const cancelQueue = useWx((s) => s.cancelQueue);
  const nonce = useWx((s) => s.composerNonce);
  const setSystem = useWx((s) => s.setSystem);
  const activeId = useWx((s) => s.activeId);
  const key = activeId ?? "__new";
  const draft = drafts[key] ?? "";
  const files = attachments[key] ?? [];
  const modelId = active?.modelId ?? pendingModelId;
  const ready = isReadyModel(providers, modelId);
  const box = useRef<HTMLTextAreaElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [contextOpen, setContextOpen] = useState(false);
  const [systemDraft, setSystemDraft] = useState("");

  useEffect(() => {
    const el = box.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 240)}px`;
  }, [draft]);

  useEffect(() => {
    if (nonce) box.current?.focus();
  }, [nonce]);

  const canSend = (draft.trim().length > 0 || files.length > 0) && ready;

  return (
    <div className="composer-wrap">
      {!ready ? <Onboarding /> : null}
      {queued ? (
        <div className="queue">
          <span>排队：{queued.text || queued.attachments.join("、")}</span>
          <button className="text-btn" type="button" onClick={cancelQueue}>
            取消
          </button>
        </div>
      ) : null}
      {active?.system ? (
        <button className="chip context-chip" type="button" onClick={() => {
          setSystemDraft(active.system);
          setContextOpen(true);
        }}>
          <span>系统指令已设置</span>
        </button>
      ) : null}
      <div className="composer">
        {files.length ? (
          <div className="chips">
            {files.map((name) => (
              <span className="chip" key={name}>
                <span>{name}</span>
                <button className="mini" type="button" aria-label={`移除 ${name}`} onClick={() => removeAttachment(name)}>
                  <X className="icon-sm" aria-hidden />
                </button>
              </span>
            ))}
          </div>
        ) : null}
        <label className="sr-only" htmlFor="composer">
          消息
        </label>
        <textarea
          id="composer"
          ref={box}
          rows={1}
          value={draft}
          placeholder={ready ? "写下一句" : "先连接一个模型，草稿会留在这里"}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault();
              if (streaming) {
                if (canSend) send();
                return;
              }
              if (canSend) send();
            }
          }}
        />
        <div className="composer-bar">
          <button className="icon-btn" type="button" aria-label="添加附件" onClick={() => fileRef.current?.click()}>
            <Paperclip className="icon" aria-hidden />
          </button>
          <input
            ref={fileRef}
            className="sr-only"
            tabIndex={-1}
            type="file"
            multiple
            aria-hidden
            onChange={(e) => {
              for (const file of e.target.files ?? []) addAttachment(file.name);
              e.target.value = "";
            }}
          />
          <Popover.Root>
            <Popover.Trigger className="model-btn" type="button">
              <span>{modelById(modelId).label}</span>
              <ChevronDown className="icon-sm" aria-hidden />
            </Popover.Trigger>
            <Popover.Portal>
              <Popover.Content className="popover" side="top" align="start" sideOffset={8}>
                {MODELS.map((m) => {
                  const ok = isReadyModel(providers, m.id);
                  return (
                    <Popover.Close key={m.id} asChild>
                      <button
                        type="button"
                        className="menu-item"
                        disabled={!ok}
                        data-active={m.id === modelId}
                        onClick={() => setModel(m.id)}
                      >
                        {m.id === modelId ? <Check className="icon-sm" aria-hidden /> : <span className="icon-sm" />}
                        <span>{ok ? m.label : `${m.label} · 未连接`}</span>
                      </button>
                    </Popover.Close>
                  );
                })}
              </Popover.Content>
            </Popover.Portal>
          </Popover.Root>
          <span className="bar-gap" />
          {streaming ? (
            <button className="text-btn" type="button" onClick={send} disabled={!canSend}>
              排队发送
            </button>
          ) : null}
          {streaming ? (
            <button className="send-btn" type="button" aria-label="停止" onClick={stop}>
              <Square className="icon-sm" aria-hidden />
            </button>
          ) : (
            <button className="send-btn" type="button" aria-label="发送" disabled={!canSend} onClick={send}>
              <ArrowUp className="icon" aria-hidden />
            </button>
          )}
        </div>
      </div>
      <p className="sr-only">Enter 发送，Shift+Enter 换行。拖放或使用附件按钮加入文件。</p>
      <Dialog
        open={contextOpen}
        title="对话设置"
        description="只影响这一次对话的系统指令。"
        onClose={() => setContextOpen(false)}
        narrow
      >
        <div className="dialog-body">
          <label className="field">
            <span>系统指令</span>
            <textarea value={systemDraft} onChange={(e) => setSystemDraft(e.target.value)} />
          </label>
          <div className="actions">
            <button
              className="btn btn-primary"
              type="button"
              onClick={() => {
                setSystem(systemDraft);
                setContextOpen(false);
              }}
            >
              保存
            </button>
            <button className="btn btn-ghost" type="button" onClick={() => setContextOpen(false)}>
              取消
            </button>
          </div>
        </div>
      </Dialog>
    </div>
  );
}
