import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppShell } from "@/components/shell/app-shell";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppShell />
  </StrictMode>,
);
