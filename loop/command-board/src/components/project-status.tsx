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
    <section className="flex h-9 shrink-0 items-center gap-3 overflow-hidden border-b border-border px-3">
      <span
        className={cn(
          "badge",
          phase === "live" && "badge-adopt",
          phase === "loop" && "badge-primary",
          phase === "review" && "badge-warn",
          (phase === "idle" || phase === "docs") && "badge-mute",
        )}
      >
        {PHASE_KO[phase]}
      </span>
      <p className="min-w-0 truncate text-sm text-foreground">{st.now}</p>
      <p className="hidden min-w-0 truncate text-sm text-muted lg:block">다음 {st.next}</p>
      <span className="ml-auto hidden shrink-0 font-mono text-xs text-subtle sm:block">{st.updated}</span>
      <button
        type="button"
        onClick={refresh}
        disabled={busy}
        className="btn-quiet shrink-0"
      >
        {busy ? "읽는 중" : "문서 갱신"}
      </button>
    </section>
  );
}
