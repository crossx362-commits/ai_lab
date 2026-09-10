import { useEffect, useState } from "react";
import { useBoardStore } from "@/lib/board-store";
import { PHASE_KO, statusOf } from "@/lib/lab-status";
import { fetchLive, mergeStatus, type LivePatch } from "@/lib/lab-live";
import { cn } from "@/lib/cn";

export function ProjectStatusCard() {
  const id = useBoardStore((s) => s.activeProjectId);
  const [live, setLive] = useState<LivePatch | undefined>(undefined);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let on = true;
    setLive(undefined);
    fetchLive(id).then((row) => {
      if (on) setLive(row);
    });
    return () => {
      on = false;
    };
  }, [id]);

  async function refresh() {
    setBusy(true);
    try {
      setLive(await fetchLive(id));
    } finally {
      setBusy(false);
    }
  }

  const st = mergeStatus(id, live);
  const phase = statusOf(id).phase;

  return (
    <div className="ml-auto hidden min-w-0 shrink items-center gap-2 overflow-hidden lg:flex">
      <span
        className={cn(
          "badge hidden md:inline-flex",
          phase === "live" && "badge-adopt",
          phase === "loop" && "badge-primary",
          phase === "review" && "badge-warn",
          (phase === "idle" || phase === "docs") && "badge-mute",
        )}
      >
        {PHASE_KO[phase]}
      </span>
      <span className="shrink-0 font-display text-sm text-subtle">📍 지금</span>
      <p className="min-w-0 truncate text-[13px] text-foreground" title={st.now + (st.next ? "\n다음: " + st.next : "")}>
        {st.now}
      </p>
      <button type="button" onClick={refresh} disabled={busy} className="btn-quiet h-7 shrink-0 px-2.5 text-[13px]" title={"마지막 갱신 " + st.updated}>
        {busy ? "읽는 중" : "다시 읽기"}
      </button>
    </div>
  );
}
