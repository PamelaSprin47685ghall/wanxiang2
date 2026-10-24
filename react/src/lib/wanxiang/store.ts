import { create } from "zustand";
import { persist } from "zustand/middleware";
import { COMPACT_MQ } from "@/lib/wanxiang/contract";
import { draftReply } from "@/lib/wanxiang/reply";
import { seedConversations, seedProviders } from "@/lib/wanxiang/seed";
import {
  modelById,
  presetByKind,
  type ChatMessage,
  type Conversation,
  type Provider,
  type ProviderKind,
  type ThemeMode,
} from "@/lib/wanxiang/types";

export type SettingsTab = "models" | "client" | "devices";

type Queue = { text: string; attachments: string[] };

type WxState = {
  theme: ThemeMode;
  railOpen: boolean;
  drawer: boolean;
  providers: Provider[];
  defaultModelId: string;
  pendingModelId: string;
  conversations: Conversation[];
  activeId: string | null;
  drafts: Record<string, string>;
  attachments: Record<string, string[]>;
  query: string;
  showArchive: boolean;
  streaming: boolean;
  queued: Queue | null;
  settings: SettingsTab | null;
  palette: boolean;
  focusMessageId: string | null;
  composerNonce: number;
  setTheme: (theme: ThemeMode) => void;
  toggleNav: () => void;
  closeDrawer: () => void;
  setQuery: (query: string) => void;
  setShowArchive: (show: boolean) => void;
  openSettings: (tab?: SettingsTab) => void;
  closeSettings: () => void;
  setPalette: (open: boolean) => void;
  clearFocus: () => void;
  newConversation: () => void;
  prefill: (text: string) => void;
  selectConversation: (id: string) => void;
  setDraft: (text: string) => void;
  addAttachment: (name: string) => void;
  removeAttachment: (name: string) => void;
  setModel: (id: string) => void;
  setDefaultModel: (id: string) => void;
  connectProvider: (kind: ProviderKind, secret: string) => string | null;
  disconnectProvider: (id: string) => void;
  setSystem: (system: string) => void;
  send: () => void;
  stop: () => void;
  cancelQueue: () => void;
  retry: (messageId: string) => void;
  regenerate: (messageId: string) => void;
  stepVersion: (messageId: string, dir: -1 | 1) => void;
  forkAt: (messageId: string) => void;
  rename: (title: string) => void;
  togglePin: () => void;
  toggleArchive: () => void;
  remove: (id: string) => void;
};

let generation = 0;
let timer: ReturnType<typeof setInterval> | null = null;
let activeStream: { convId: string; messageId: string } | null = null;

function draftKey(activeId: string | null) {
  return activeId ?? "__new";
}

function uid() {
  return crypto.randomUUID();
}

function titleFrom(text: string, attachments: string[]) {
  const line = text.trim().split("\n")[0] ?? "";
  const base = line || attachments[0] || "新对话";
  return base.length > 24 ? `${base.slice(0, 24)}…` : base;
}

function modelReady(providers: Provider[], modelId: string) {
  const model = modelById(modelId);
  return providers.some((p) => p.connected && p.kind === model.kind);
}

function patchConversations(
  conversations: Conversation[],
  convId: string,
  mapMessage: (m: ChatMessage) => ChatMessage,
  touch: boolean,
) {
  return conversations.map((c) =>
    c.id !== convId
      ? c
      : {
          ...c,
          updatedAt: touch ? Date.now() : c.updatedAt,
          messages: c.messages.map(mapMessage),
        },
  );
}

export const useWx = create<WxState>()(
  persist(
    (set, get) => {
      function halt() {
        generation += 1;
        if (timer) clearInterval(timer);
        timer = null;
      }

      function writeSlice(convId: string, messageId: string, slice: string) {
        set((s) => ({
          conversations: patchConversations(
            s.conversations,
            convId,
            (m) => {
              if (m.id !== messageId) return m;
              const versions = m.versions
                ? m.versions.map((v, i) => (i === (m.versionIndex ?? 0) ? slice : v))
                : m.versions;
              return { ...m, text: slice, versions };
            },
            false,
          ),
        }));
      }

      function finishStream() {
        if (timer) clearInterval(timer);
        timer = null;
        activeStream = null;
        const queued = get().queued;
        set({ streaming: false, queued: null });
        if (queued && (queued.text.trim() || queued.attachments.length)) {
          commit(queued.text, queued.attachments);
        }
      }

      function startStream(convId: string, messageId: string, full: string) {
        halt();
        const token = generation;
        activeStream = { convId, messageId };
        set({ streaming: true });
        let i = 0;
        timer = setInterval(() => {
          if (token !== generation) return;
          const still = get().conversations.some((c) => c.id === convId);
          if (!still) {
            halt();
            set({ streaming: false });
            return;
          }
          i = Math.min(full.length, i + 4);
          writeSlice(convId, messageId, full.slice(0, i));
          if (i >= full.length) finishStream();
        }, 16);
      }

      function commit(text: string, attachments: string[]) {
        const state = get();
        const modelId = state.activeId
          ? (state.conversations.find((c) => c.id === state.activeId)?.modelId ?? state.pendingModelId)
          : state.pendingModelId;
        if (!modelReady(state.providers, modelId)) return;
        const now = Date.now();
        const user: ChatMessage = {
          id: uid(),
          role: "user",
          text,
          createdAt: now,
          attachments: attachments.length ? attachments : undefined,
        };
        const reply = draftReply(text || attachments.join(" "));
        const assistant: ChatMessage = {
          id: uid(),
          role: "assistant",
          text: "",
          createdAt: now + 1,
          tool: reply.tool,
        };
        let convId = state.activeId;
        if (!convId) {
          convId = uid();
          const conv: Conversation = {
            id: convId,
            title: titleFrom(text, attachments),
            pinned: false,
            archived: false,
            createdAt: now,
            updatedAt: now,
            modelId,
            system: "",
            forkedFrom: null,
            messages: [user, assistant],
          };
          set((s) => ({
            conversations: [conv, ...s.conversations],
            activeId: convId,
            drafts: { ...s.drafts, __new: "" },
            attachments: { ...s.attachments, __new: [] },
            composerNonce: s.composerNonce + 1,
          }));
        } else {
          set((s) => ({
            conversations: s.conversations.map((c) =>
              c.id !== convId
                ? c
                : {
                    ...c,
                    updatedAt: now,
                    messages: [...c.messages, user, assistant],
                  },
            ),
            drafts: { ...s.drafts, [convId!]: "" },
            attachments: { ...s.attachments, [convId!]: [] },
          }));
        }
        startStream(convId, assistant.id, reply.text);
      }

      return {
        theme: "system",
        railOpen: true,
        drawer: false,
        providers: seedProviders,
        defaultModelId: "qwen-local",
        pendingModelId: "qwen-local",
        conversations: seedConversations,
        activeId: null,
        drafts: {},
        attachments: {},
        query: "",
        showArchive: false,
        streaming: false,
        queued: null,
        settings: null,
        palette: false,
        focusMessageId: null,
        composerNonce: 0,
        setTheme: (theme) => set({ theme }),
        toggleNav: () => {
          if (typeof window !== "undefined" && window.matchMedia(COMPACT_MQ).matches) {
            set((s) => ({ drawer: !s.drawer }));
          } else {
            set((s) => ({ railOpen: !s.railOpen }));
          }
        },
        closeDrawer: () => set({ drawer: false }),
        setQuery: (query) => set({ query }),
        setShowArchive: (showArchive) => set({ showArchive }),
        openSettings: (tab = "models") => set({ settings: tab, palette: false, drawer: false }),
        closeSettings: () => set({ settings: null }),
        setPalette: (palette) => set({ palette }),
        clearFocus: () => set({ focusMessageId: null }),
        newConversation: () =>
          set((s) => ({
            activeId: null,
            pendingModelId: s.defaultModelId,
            drawer: false,
            showArchive: false,
            composerNonce: s.composerNonce + 1,
          })),
        prefill: (text) => {
          const key = draftKey(get().activeId);
          set((s) => ({
            drafts: { ...s.drafts, [key]: text },
            composerNonce: s.composerNonce + 1,
          }));
        },
        selectConversation: (id) => {
          const conv = get().conversations.find((c) => c.id === id);
          let focus: string | null = null;
          const q = get().query.trim().toLowerCase();
          if (conv && q) {
            focus = conv.messages.find((m) => m.text.toLowerCase().includes(q))?.id ?? null;
          }
          set({ activeId: id, drawer: false, focusMessageId: focus });
        },
        setDraft: (text) => {
          const key = draftKey(get().activeId);
          set((s) => ({ drafts: { ...s.drafts, [key]: text } }));
        },
        addAttachment: (name) => {
          const key = draftKey(get().activeId);
          set((s) => {
            const cur = s.attachments[key] ?? [];
            if (cur.length >= 6 || cur.includes(name)) return s;
            return { attachments: { ...s.attachments, [key]: [...cur, name] } };
          });
        },
        removeAttachment: (name) => {
          const key = draftKey(get().activeId);
          set((s) => ({
            attachments: {
              ...s.attachments,
              [key]: (s.attachments[key] ?? []).filter((n) => n !== name),
            },
          }));
        },
        setModel: (id) => {
          const { activeId } = get();
          if (!modelReady(get().providers, id)) return;
          if (!activeId) set({ pendingModelId: id });
          else {
            set((s) => ({
              conversations: s.conversations.map((c) => (c.id === activeId ? { ...c, modelId: id } : c)),
            }));
          }
        },
        setDefaultModel: (id) =>
          set((s) => ({
            defaultModelId: id,
            pendingModelId: s.activeId ? s.pendingModelId : id,
          })),
        connectProvider: (kind, secret) => {
          const trimmed = secret.trim();
          if (kind !== "ollama" && trimmed.length < 8) return "密钥至少 8 位。不会发到模型服务。";
          const preset = presetByKind(kind);
          const hint = kind === "ollama" ? null : trimmed.slice(-4);
          set((s) => {
            const existing = s.providers.find((p) => p.kind === kind);
            const next: Provider = existing
              ? { ...existing, connected: true, keyHint: hint, name: kind === "ollama" ? "本机 Ollama" : preset.name }
              : {
                  id: uid(),
                  kind,
                  name: kind === "ollama" ? "本机 Ollama" : preset.name,
                  connected: true,
                  keyHint: hint,
                };
            const providers = existing
              ? s.providers.map((p) => (p.kind === kind ? next : p))
              : [...s.providers, next];
            const model = modelById(
              kind === "anthropic" ? "claude-sonnet" : kind === "openai" ? "gpt" : kind === "gemini" ? "gemini" : "qwen-local",
            );
            return {
              providers,
              defaultModelId: model.id,
              pendingModelId: s.activeId ? s.pendingModelId : model.id,
            };
          });
          return null;
        },
        disconnectProvider: (id) =>
          set((s) => ({
            providers: s.providers.map((p) => (p.id === id ? { ...p, connected: false, keyHint: null } : p)),
          })),
        setSystem: (system) => {
          const { activeId } = get();
          if (!activeId) return;
          set((s) => ({
            conversations: s.conversations.map((c) => (c.id === activeId ? { ...c, system } : c)),
          }));
        },
        send: () => {
          const state = get();
          const key = draftKey(state.activeId);
          const text = state.drafts[key] ?? "";
          const attachments = state.attachments[key] ?? [];
          if (!text.trim() && attachments.length === 0) return;
          const modelId = state.activeId
            ? (state.conversations.find((c) => c.id === state.activeId)?.modelId ?? state.pendingModelId)
            : state.pendingModelId;
          if (!modelReady(state.providers, modelId)) {
            set({ settings: "models" });
            return;
          }
          if (state.streaming) {
            set({
              queued: { text, attachments },
              drafts: { ...state.drafts, [key]: "" },
              attachments: { ...state.attachments, [key]: [] },
            });
            return;
          }
          commit(text, attachments);
        },
        stop: () => {
          const current = activeStream;
          halt();
          activeStream = null;
          const state = get();
          const key = draftKey(state.activeId);
          const queued = state.queued;
          set((s) => ({
            streaming: false,
            queued: null,
            drafts:
              queued && !(s.drafts[key] ?? "").trim()
                ? { ...s.drafts, [key]: queued.text }
                : s.drafts,
            attachments:
              queued && !(s.attachments[key] ?? []).length
                ? { ...s.attachments, [key]: queued.attachments }
                : s.attachments,
            conversations: current
              ? s.conversations.map((c) =>
                  c.id !== current.convId
                    ? c
                    : {
                        ...c,
                        messages: c.messages.map((m) =>
                          m.id === current.messageId && !m.text ? { ...m, text: "已停止。" } : m,
                        ),
                      },
                )
              : s.conversations,
          }));
        },
        cancelQueue: () => {
          const state = get();
          if (!state.queued) return;
          const key = draftKey(state.activeId);
          const queued = state.queued;
          set((s) => ({
            queued: null,
            drafts: { ...s.drafts, [key]: queued.text },
            attachments: { ...s.attachments, [key]: queued.attachments },
          }));
        },
        retry: (messageId) => {
          const state = get();
          if (state.streaming || !state.activeId) return;
          const conv = state.conversations.find((c) => c.id === state.activeId);
          if (!conv) return;
          const index = conv.messages.findIndex((m) => m.id === messageId);
          if (index < 1) return;
          const prior = conv.messages[index - 1];
          const reply = draftReply(prior?.text ?? "");
          set((s) => ({
            conversations: s.conversations.map((c) =>
              c.id !== conv.id
                ? c
                : {
                    ...c,
                    messages: c.messages.map((m) =>
                      m.id === messageId ? { ...m, error: undefined, text: "", tool: reply.tool } : m,
                    ),
                  },
            ),
          }));
          startStream(conv.id, messageId, reply.text);
        },
        regenerate: (messageId) => {
          const state = get();
          if (state.streaming || !state.activeId) return;
          const conv = state.conversations.find((c) => c.id === state.activeId);
          if (!conv) return;
          const index = conv.messages.findIndex((m) => m.id === messageId);
          const message = conv.messages[index];
          if (!message || message.role !== "assistant" || index !== conv.messages.length - 1) return;
          const prior = [...conv.messages].reverse().find((m) => m.role === "user");
          const reply = draftReply(prior?.text ?? "");
          const base = message.versions ?? [message.text];
          const versions = [...base, ""];
          set((s) => ({
            conversations: s.conversations.map((c) =>
              c.id !== conv.id
                ? c
                : {
                    ...c,
                    messages: c.messages.map((m) =>
                      m.id === messageId
                        ? {
                            ...m,
                            versions,
                            versionIndex: versions.length - 1,
                            text: "",
                            error: undefined,
                            tool: reply.tool ?? m.tool,
                          }
                        : m,
                    ),
                  },
            ),
          }));
          startStream(conv.id, messageId, reply.text);
        },
        stepVersion: (messageId, dir) => {
          if (get().streaming) return;
          set((s) => ({
            conversations: s.conversations.map((c) =>
              c.id !== s.activeId
                ? c
                : {
                    ...c,
                    messages: c.messages.map((m) => {
                      if (m.id !== messageId || !m.versions?.length) return m;
                      const next = Math.min(m.versions.length - 1, Math.max(0, (m.versionIndex ?? 0) + dir));
                      return { ...m, versionIndex: next, text: m.versions[next] ?? m.text };
                    }),
                  },
            ),
          }));
        },
        forkAt: (messageId) => {
          const state = get();
          if (!state.activeId) return;
          const conv = state.conversations.find((c) => c.id === state.activeId);
          if (!conv) return;
          const index = conv.messages.findIndex((m) => m.id === messageId);
          if (index < 0) return;
          const now = Date.now();
          const created: Conversation = {
            id: uid(),
            title: titleFrom(`分叉：${conv.title}`, []),
            pinned: false,
            archived: false,
            createdAt: now,
            updatedAt: now,
            modelId: conv.modelId,
            system: conv.system,
            forkedFrom: { id: conv.id, title: conv.title, at: index + 1 },
            messages: conv.messages.slice(0, index + 1).map((m) => ({ ...m, id: uid() })),
          };
          set((s) => ({
            conversations: [created, ...s.conversations],
            activeId: created.id,
            drawer: false,
          }));
        },
        rename: (title) => {
          const next = title.trim();
          const { activeId } = get();
          if (!next || !activeId) return;
          set((s) => ({
            conversations: s.conversations.map((c) => (c.id === activeId ? { ...c, title: next } : c)),
          }));
        },
        togglePin: () => {
          const { activeId } = get();
          if (!activeId) return;
          set((s) => ({
            conversations: s.conversations.map((c) => (c.id === activeId ? { ...c, pinned: !c.pinned } : c)),
          }));
        },
        toggleArchive: () => {
          const { activeId } = get();
          if (!activeId) return;
          set((s) => ({
            activeId: null,
            conversations: s.conversations.map((c) =>
              c.id === activeId ? { ...c, archived: !c.archived, pinned: c.archived ? c.pinned : false } : c,
            ),
          }));
        },
        remove: (id) =>
          set((s) => ({
            conversations: s.conversations.filter((c) => c.id !== id),
            activeId: s.activeId === id ? null : s.activeId,
          })),
      };
    },
    {
      name: "wanxiang-ui",
      skipHydration: true,
      partialize: (s) => ({
        theme: s.theme,
        railOpen: s.railOpen,
        providers: s.providers,
        defaultModelId: s.defaultModelId,
        pendingModelId: s.pendingModelId,
        conversations: s.conversations,
        activeId: s.activeId,
        drafts: s.drafts,
        attachments: s.attachments,
      }),
    },
  ),
);

export function useActive() {
  return useWx((s) => s.conversations.find((c) => c.id === s.activeId) ?? null);
}

export function useDraftKey() {
  const activeId = useWx((s) => s.activeId);
  return draftKey(activeId);
}

export function isReadyModel(providers: Provider[], modelId: string) {
  return modelReady(providers, modelId);
}
