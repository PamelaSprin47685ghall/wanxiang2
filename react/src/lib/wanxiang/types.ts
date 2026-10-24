export type ThemeMode = "light" | "dark" | "system";

export type ProviderKind = "openai" | "anthropic" | "gemini" | "ollama";

export type Provider = {
  id: string;
  kind: ProviderKind;
  name: string;
  connected: boolean;
  keyHint: string | null;
};

export type ToolCard = {
  summary: string;
  detail: string;
};

export type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  text: string;
  createdAt: number;
  attachments?: string[];
  tool?: ToolCard;
  error?: string;
  versions?: string[];
  versionIndex?: number;
};

export type Conversation = {
  id: string;
  title: string;
  pinned: boolean;
  archived: boolean;
  createdAt: number;
  updatedAt: number;
  modelId: string;
  system: string;
  forkedFrom: { id: string; title: string; at: number } | null;
  messages: ChatMessage[];
};

export type ModelOption = {
  id: string;
  kind: ProviderKind;
  label: string;
};

export const MODELS: ModelOption[] = [
  { id: "claude-sonnet", kind: "anthropic", label: "Claude Sonnet" },
  { id: "gpt", kind: "openai", label: "GPT" },
  { id: "gemini", kind: "gemini", label: "Gemini" },
  { id: "qwen-local", kind: "ollama", label: "本地 Qwen" },
];

export const PRESETS: { kind: ProviderKind; name: string; blurb: string }[] = [
  { kind: "openai", name: "OpenAI", blurb: "密钥" },
  { kind: "anthropic", name: "Claude", blurb: "密钥" },
  { kind: "gemini", name: "Gemini", blurb: "密钥" },
  { kind: "ollama", name: "本机 Ollama", blurb: "无需密钥" },
];

export function modelById(id: string) {
  return MODELS.find((m) => m.id === id) ?? MODELS[3];
}

export function presetByKind(kind: ProviderKind) {
  return PRESETS.find((p) => p.kind === kind) ?? PRESETS[0];
}
