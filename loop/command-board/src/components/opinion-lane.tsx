import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import { AI_ROSTER } from "@/lib/ai-roster";
import type { BoardCard as Card } from "@/lib/board-types";
import { cn } from "@/lib/cn";

function GlanceCard({ card }: { card: Card }) {
  const decide = useBoardStore((s) => s.decide);
  const busy = useBoardStore((s) => s.busy);
  const running = card.status === "running";
  const failed = card.status === "fail";
  const open = !card.verdict && !running && !failed;

  return (
    <article
      title={card.body}
      className={cn(
        "flex min-w-0 items-center gap-2 rounded-lg border border-border bg-raised px-2 py-1.5",
        card.verdict === "채택" && "border-adopt/50",
        card.verdict === "반려" && "opacity-40",
        failed && "border-dashed opacity-60",
      )}
    >
      <div className="min-w-0 flex-1">
        <p className="text-xs text-subtle">{card.who}</p>
        <h3 className="truncate text-sm font-medium text-foreground">
          {running ? <span className="animate-pulse text-subtle">응답 대기…</span> : card.title}
        </h3>
      </div>
      {open ? (
        <div className="flex shrink-0 gap-1">
          <Button type="button" variant="adopt" size="sm" disabled={busy} onClick={() => void decide(card.id, "채택")}>
            채택
          </Button>
          <Button type="button" variant="reject" size="sm" disabled={busy} onClick={() => void decide(card.id, "반려")}>
            반려
          </Button>
        </div>
      ) : (
        <span className="shrink-0 text-xs text-subtle">{failed ? "실패" : card.verdict || ""}</span>
      )}
    </article>
  );
}

export function OpinionLane() {
  const cards = useBoardStore((s) => s.cards);
  const gather = useBoardStore((s) => s.gatherOpinions);
  const busy = useBoardStore((s) => s.busy);
  const running = useBoardStore((s) => s.dispatchRunning);
  const hasCommand = cards.some((c) => c.col === "명령");
  const list = cards.filter((c) => c.col === "의견");
  const open = list.filter((c) => !c.verdict);
  const ordered = AI_ROSTER.map((name) => list.find((c) => c.who === name)).filter((c): c is Card => Boolean(c));
  const extra = list.filter((c) => !AI_ROSTER.includes(c.who as (typeof AI_ROSTER)[number]));
  const glance = ordered.concat(extra).slice(0, 5);

  return (
    <section className="shrink-0 border-b border-border bg-elevated px-3 py-1.5">
      <div className="mb-1.5 flex items-center gap-3">
        <h2 className="text-sm font-medium tracking-tight">의견</h2>
        <span className="font-mono text-xs tabular-nums text-muted">{open.length} 대기</span>
        {running ? <span className="animate-pulse text-xs text-subtle">CLI 응답 수집 중</span> : null}
        <button
          type="button"
          onClick={() => void gather()}
          disabled={busy || running || !hasCommand}
          className="ml-auto h-8 rounded-full border border-border bg-raised px-3 text-xs text-foreground disabled:opacity-50"
        >
          {running ? "수집 중…" : "의견 다시 받기"}
        </button>
      </div>
      {glance.length === 0 ? (
        <p className="rounded-lg border border-dashed border-border px-3 py-2 text-center text-sm text-subtle">
          {hasCommand ? (running ? "CLI 응답을 기다리는 중" : "의견 없음 — 「의견 다시 받기」") : "명령을 내리면 CLI 4종이 의견을 낸다"}
        </p>
      ) : (
        <div className="grid grid-cols-5 gap-1.5">
          {glance.map((card) => (
            <GlanceCard key={card.id} card={card} />
          ))}
        </div>
      )}
    </section>
  );
}
