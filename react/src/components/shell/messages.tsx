import { Check, ChevronDown, ChevronLeft, ChevronRight, Copy, GitFork, RefreshCw, Wrench } from "lucide-react";
import { useState, type ReactNode } from "react";
import type { ChatMessage } from "@/lib/wanxiang/types";
import { useWx } from "@/lib/wanxiang/store";

function renderInline(text: string, keyBase: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  const re = /`([^`]+)`/g;
  let last = 0;
  let n = 0;
  for (const match of text.matchAll(re)) {
    const index = match.index ?? 0;
    if (index > last) nodes.push(text.slice(last, index));
    nodes.push(
      <code key={`${keyBase}-c-${n++}`} className="inline-code">
        {match[1]}
      </code>,
    );
    last = index + match[0].length;
  }
  if (last < text.length) nodes.push(text.slice(last));
  const lines: ReactNode[] = [];
  nodes.forEach((node, i) => {
    if (typeof node !== "string") {
      lines.push(node);
      return;
    }
    const parts = node.split("\n");
    parts.forEach((part, j) => {
      if (j > 0) lines.push(<br key={`${keyBase}-b-${i}-${j}`} />);
      if (part) lines.push(part);
    });
  });
  return lines;
}

function RichText({ text }: { text: string }) {
  const re = /```([^\n`]*)\n([\s\S]*?)```/g;
  const nodes: ReactNode[] = [];
  let last = 0;
  let n = 0;
  for (const match of text.matchAll(re)) {
    const index = match.index ?? 0;
    if (index > last) nodes.push(<Prose key={`p-${n}`} text={text.slice(last, index)} id={`p-${n}`} />);
    nodes.push(
      <pre key={`pre-${n}`} className="codeblock">
        <code>{match[2].replace(/\n$/, "")}</code>
      </pre>,
    );
    n += 1;
    last = index + match[0].length;
  }
  if (last < text.length) nodes.push(<Prose key={`p-${n}`} text={text.slice(last)} id={`p-${n}`} />);
  return <div className="prose">{nodes}</div>;
}

function Prose({ text, id }: { text: string; id: string }) {
  const blocks = text.split(/\n{2,}/).filter((b) => b.trim().length > 0);
  return blocks.map((block, i) => <p key={`${id}-${i}`}>{renderInline(block, `${id}-${i}`)}</p>);
}

function ToolCard({ summary, detail }: { summary: string; detail: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="tool">
      <button className="tool-toggle" aria-expanded={open} onClick={() => setOpen((v) => !v)}>
        <Wrench className="icon-sm" aria-hidden />
        <span>{summary}</span>
        <ChevronDown className="icon-sm chev" data-open={open ? "true" : "false"} aria-hidden />
      </button>
      {open ? (
        <pre className="codeblock">
          <code>{detail}</code>
        </pre>
      ) : null}
    </div>
  );
}

export function MessageList({ messages, streaming }: { messages: ChatMessage[]; streaming: boolean }) {
  const retry = useWx((s) => s.retry);
  const regenerate = useWx((s) => s.regenerate);
  const forkAt = useWx((s) => s.forkAt);
  const stepVersion = useWx((s) => s.stepVersion);
  const [copied, setCopied] = useState<string | null>(null);

  async function copy(id: string, text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(id);
      window.setTimeout(() => setCopied((cur) => (cur === id ? null : cur)), 1200);
    } catch {
      setCopied(null);
    }
  }

  return (
    <div className="column">
      {messages.map((m, index) => {
        const last = index === messages.length - 1;
        const versions = m.versions;
        const versionIndex = m.versionIndex ?? 0;
        return (
          <article key={m.id} id={`msg-${m.id}`} className={m.role === "user" ? "msg msg-user" : "msg"}>
            {m.role === "assistant" && m.tool ? <ToolCard summary={m.tool.summary} detail={m.tool.detail} /> : null}
            {m.role === "user" ? (
              <div className="bubble">
                {m.text ? <RichText text={m.text} /> : null}
                {m.attachments?.length ? (
                  <div className="chips">
                    {m.attachments.map((name) => (
                      <span className="chip" key={name}>
                        <span>{name}</span>
                      </span>
                    ))}
                  </div>
                ) : null}
              </div>
            ) : m.error ? (
              <div className="alert" role="alert">
                <p>{m.error}</p>
                <div className="actions">
                  <button className="btn btn-ghost" type="button" onClick={() => retry(m.id)} disabled={streaming}>
                    <RefreshCw className="icon-sm" aria-hidden />
                    重试生成
                  </button>
                </div>
              </div>
            ) : m.text ? (
              <RichText text={m.text} />
            ) : streaming && last ? (
              <p>
                <span className="caret" aria-hidden />
                <span className="sr-only">正在回复</span>
              </p>
            ) : null}
            <div className="msg-actions">
              {m.text ? (
                <button className="mini" type="button" onClick={() => copy(m.id, m.text)}>
                  {copied === m.id ? <Check className="icon-sm" aria-hidden /> : <Copy className="icon-sm" aria-hidden />}
                  {copied === m.id ? "已复制" : "复制"}
                </button>
              ) : null}
              <button className="mini" type="button" onClick={() => forkAt(m.id)}>
                <GitFork className="icon-sm" aria-hidden />
                分叉
              </button>
              {m.role === "assistant" && last && !m.error ? (
                <button className="mini" type="button" onClick={() => regenerate(m.id)} disabled={streaming}>
                  <RefreshCw className="icon-sm" aria-hidden />
                  重新生成
                </button>
              ) : null}
              {versions && versions.length > 1 ? (
                <span className="versions">
                  <button
                    className="mini"
                    type="button"
                    aria-label="上一版本"
                    disabled={streaming || versionIndex <= 0}
                    onClick={() => stepVersion(m.id, -1)}
                  >
                    <ChevronLeft className="icon-sm" aria-hidden />
                  </button>
                  {versionIndex + 1}/{versions.length}
                  <button
                    className="mini"
                    type="button"
                    aria-label="下一版本"
                    disabled={streaming || versionIndex >= versions.length - 1}
                    onClick={() => stepVersion(m.id, 1)}
                  >
                    <ChevronRight className="icon-sm" aria-hidden />
                  </button>
                </span>
              ) : null}
            </div>
          </article>
        );
      })}
    </div>
  );
}
