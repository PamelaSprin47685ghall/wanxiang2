import { useEffect } from "react";
import { CommandPalette } from "@/components/shell/command-palette";
import { SettingsDialog } from "@/components/shell/settings-dialog";
import { Sidebar } from "@/components/shell/sidebar";
import { Stage } from "@/components/shell/stage";
import { useWx } from "@/lib/wanxiang/store";
import type { ThemeMode } from "@/lib/wanxiang/types";

function applyTheme(theme: ThemeMode) {
  const dark =
    theme === "dark" ||
    (theme === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
  document.documentElement.classList.toggle("dark", dark);
  document.documentElement.style.colorScheme = dark ? "dark" : "light";
}

export function AppShell() {
  const theme = useWx((s) => s.theme);
  const railOpen = useWx((s) => s.railOpen);
  const drawer = useWx((s) => s.drawer);
  const closeDrawer = useWx((s) => s.closeDrawer);
  const setPalette = useWx((s) => s.setPalette);
  const closeSettings = useWx((s) => s.closeSettings);
  const create = useWx((s) => s.newConversation);

  useEffect(() => {
    void useWx.persist.rehydrate();
  }, []);

  useEffect(() => {
    applyTheme(theme);
    if (theme !== "system") return;
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const on = () => applyTheme("system");
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, [theme]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.isComposing) return;
      const meta = e.metaKey || e.ctrlKey;
      if (meta && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPalette(!useWx.getState().palette);
      }
      if (meta && e.key.toLowerCase() === "n") {
        e.preventDefault();
        create();
      }
      if (e.key === "Escape") {
        const state = useWx.getState();
        if (state.palette) setPalette(false);
        else if (state.settings) closeSettings();
        else if (state.drawer) closeDrawer();
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [closeDrawer, closeSettings, create, setPalette]);

  return (
    <div className="shell" data-app="wanxiang" data-rail={railOpen ? "open" : "closed"} data-drawer={drawer ? "open" : "closed"}>
      <button className="scrim" type="button" aria-label="关闭对话列表" onClick={closeDrawer} tabIndex={drawer ? 0 : -1} />
      <Sidebar />
      <Stage />
      <SettingsDialog />
      <CommandPalette />
    </div>
  );
}
