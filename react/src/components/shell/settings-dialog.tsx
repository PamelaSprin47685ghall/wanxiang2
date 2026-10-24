import { useEffect, useState } from "react";
import { Dialog } from "@/components/shell/dialog";
import { MODELS, PRESETS, type ProviderKind, type ThemeMode } from "@/lib/wanxiang/types";
import { isReadyModel, useWx } from "@/lib/wanxiang/store";

const THEMES: { id: ThemeMode; label: string }[] = [
  { id: "system", label: "跟随系统" },
  { id: "light", label: "浅色" },
  { id: "dark", label: "深色" },
];

export function SettingsDialog() {
  const tab = useWx((s) => s.settings);
  const close = useWx((s) => s.closeSettings);
  const openSettings = useWx((s) => s.openSettings);
  const providers = useWx((s) => s.providers);
  const connect = useWx((s) => s.connectProvider);
  const disconnect = useWx((s) => s.disconnectProvider);
  const theme = useWx((s) => s.theme);
  const setTheme = useWx((s) => s.setTheme);
  const defaultModelId = useWx((s) => s.defaultModelId);
  const setDefaultModel = useWx((s) => s.setDefaultModel);
  const [secret, setSecret] = useState("");
  const [kind, setKind] = useState<ProviderKind | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [checked, setChecked] = useState(false);
  const [code, setCode] = useState<string | null>(null);
  const [left, setLeft] = useState(0);

  useEffect(() => {
    if (!code) return;
    const started = Date.now();
    const total = 5 * 60;
    const id = window.setInterval(() => {
      const remain = total - Math.floor((Date.now() - started) / 1000);
      if (remain <= 0) {
        setCode(null);
        setLeft(0);
        window.clearInterval(id);
        return;
      }
      setLeft(remain);
    }, 250);
    return () => window.clearInterval(id);
  }, [code]);

  const connected = providers.filter((p) => p.connected);
  const mm = String(Math.floor(left / 60)).padStart(1, "0");
  const ss = String(left % 60).padStart(2, "0");

  return (
    <Dialog open={tab !== null} title="设置" description="维护模型服务、这台客户端和设备。" onClose={close}>
      <div className="tabs" role="tablist">
        {(
          [
            ["models", "模型服务"],
            ["client", "这台客户端"],
            ["devices", "设备"],
          ] as const
        ).map(([id, label]) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={tab === id}
            onClick={() => openSettings(id)}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="dialog-body">
        {tab === "models" ? (
          <div className="stack">
            <p className="hint">常用四家放在这里。保存和聊天里的连接卡片走同一条动作，不会有两套结果。</p>
            <div className="preset-grid">
              {PRESETS.map((p) => {
                const hit = providers.find((item) => item.kind === p.kind && item.connected);
                return (
                  <button
                    key={p.kind}
                    type="button"
                    className="preset"
                    aria-pressed={kind === p.kind}
                    onClick={() => {
                      setKind(p.kind);
                      setError(null);
                    }}
                  >
                    <strong>{p.name}</strong>
                    <small>{hit ? `已连接${hit.keyHint ? ` · 尾号 ${hit.keyHint}` : ""}` : p.blurb}</small>
                  </button>
                );
              })}
            </div>
            {kind ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  const message = connect(kind, secret);
                  setError(message);
                  if (!message) setSecret("");
                }}
              >
                {kind !== "ollama" ? (
                  <label className="field">
                    <span>密钥</span>
                    <input
                      type="password"
                      autoComplete="off"
                      value={secret}
                      onChange={(e) => setSecret(e.target.value)}
                      placeholder="粘贴密钥"
                    />
                  </label>
                ) : (
                  <p className="hint">本机演示不需要密钥，回复仍不出网。</p>
                )}
                {error ? (
                  <p className="form-error" role="alert">
                    {error}
                  </p>
                ) : null}
                <div className="actions">
                  <button className="btn btn-primary" type="submit">
                    {kind === "ollama" ? "使用本机" : "保存并使用"}
                  </button>
                  {providers.some((p) => p.kind === kind && p.connected) ? (
                    <button
                      className="btn btn-ghost"
                      type="button"
                      onClick={() => {
                        const hit = providers.find((p) => p.kind === kind);
                        if (hit) disconnect(hit.id);
                      }}
                    >
                      断开
                    </button>
                  ) : null}
                </div>
              </form>
            ) : null}
            <div>
              <p className="block-label">新对话的默认模型</p>
              {MODELS.filter((m) => isReadyModel(providers, m.id)).length === 0 ? (
                <p className="hint">还没有已连接的模型。</p>
              ) : (
                MODELS.filter((m) => isReadyModel(providers, m.id)).map((m) => (
                  <label className="choice" key={m.id}>
                    <input
                      type="radio"
                      name="default-model"
                      checked={defaultModelId === m.id}
                      onChange={() => setDefaultModel(m.id)}
                    />
                    <span>{m.label}</span>
                  </label>
                ))
              )}
            </div>
            <div>
              <button className="btn btn-ghost" type="button" onClick={() => setChecked(true)}>
                检查主机
              </button>
              {checked ? (
                <ul className="check-list">
                  <li>界面在浏览器里排版，不再画到画布上。</li>
                  <li>回复停在这台浏览器，没有出网。</li>
                  <li>已连接 {connected.length} 个模型服务。</li>
                </ul>
              ) : null}
            </div>
          </div>
        ) : null}
        {tab === "client" ? (
          <div className="stack">
            <p className="hint">外观立刻生效，只留在这台浏览器。移开焦点不会去改主机。</p>
            <div>
              <p className="block-label">外观</p>
              <div className="segment" role="group" aria-label="外观">
                {THEMES.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    aria-pressed={theme === item.id}
                    onClick={() => setTheme(item.id)}
                  >
                    {item.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : null}
        {tab === "devices" ? (
          <div className="stack">
            <p className="hint">这台浏览器已经在演示主机上。添加设备会给出 6 位码和倒计时，不会写进终端。</p>
            <div className="choice">
              <span className="dot" />
              <span>浏览器预览 · 当前这台</span>
            </div>
            {code ? (
              <>
                <div className="pair-code" aria-label={`配对码 ${code}`}>
                  {code.split("").map((d, i) => (
                    <span key={i}>{d}</span>
                  ))}
                </div>
                <p className="hint center">剩余 {mm}:{ss} · 演示码，不会发到真主机</p>
              </>
            ) : (
              <div className="actions">
                <button
                  className="btn btn-primary"
                  type="button"
                  onClick={() => {
                    const next = String(Math.floor(100000 + Math.random() * 900000));
                    setCode(next);
                    setLeft(5 * 60);
                  }}
                >
                  添加设备
                </button>
              </div>
            )}
          </div>
        ) : null}
      </div>
    </Dialog>
  );
}
