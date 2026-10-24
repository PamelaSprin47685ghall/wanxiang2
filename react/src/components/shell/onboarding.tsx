import { useState } from "react";
import { PRESETS, type ProviderKind } from "@/lib/wanxiang/types";
import { useWx } from "@/lib/wanxiang/store";

export function Onboarding() {
  const connect = useWx((s) => s.connectProvider);
  const [kind, setKind] = useState<ProviderKind | null>(null);
  const [secret, setSecret] = useState("");
  const [error, setError] = useState<string | null>(null);

  function submit(next: ProviderKind) {
    const message = connect(next, secret);
    setError(message);
    if (!message) {
      setSecret("");
      setKind(null);
    }
  }

  return (
    <section className="connect" aria-label="连接一个模型">
      <h2>连接一个模型</h2>
      <p className="sub">选一家。密钥只留在这台浏览器，预览不会把它发出去。</p>
      <div className="preset-grid">
        {PRESETS.map((p) => (
          <button
            key={p.kind}
            type="button"
            className="preset"
            aria-pressed={kind === p.kind}
            onClick={() => {
              setKind(p.kind);
              setError(null);
              if (p.kind === "ollama") submit("ollama");
            }}
          >
            <strong>{p.name}</strong>
            <small>{p.blurb}</small>
          </button>
        ))}
      </div>
      {kind && kind !== "ollama" ? (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            submit(kind);
          }}
        >
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
          {error ? (
            <p className="form-error" role="alert">
              {error}
            </p>
          ) : null}
          <div className="actions">
            <button className="btn btn-primary" type="submit">
              保存并使用
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}
