import { useEffect, useState } from "react";
import { COMPACT_MQ, Metric, composerPlacement } from "@/lib/wanxiang/contract";

export function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia(query);
    const on = () => setMatches(mq.matches);
    on();
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, [query]);
  return matches;
}

function useWindowHeight() {
  const [height, setHeight] = useState<number>(Metric.startWorkspaceMinHeight);
  useEffect(() => {
    const read = () => setHeight(window.innerHeight);
    read();
    window.addEventListener("resize", read);
    return () => window.removeEventListener("resize", read);
  }, []);
  return height;
}

/** ShellDesk 的布局投影。侧栏开合仍是状态，断点和起步留白只问这一处。 */
export function useShellLayout(messageCount: number, running: boolean) {
  const compact = useMediaQuery(COMPACT_MQ);
  const height = useWindowHeight();
  return {
    compact,
    placement: composerPlacement(height, messageCount, running),
  };
}
