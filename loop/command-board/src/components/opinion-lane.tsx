import { useBoardStore } from "@/lib/board-store";
import { OPINION_ROSTER, AI_ROLE, type AiName } from "@/lib/ai-roster";
import { DecideButtons } from "@/components/decide-buttons";
import type { BoardCard as Card } from "@/lib/board-types";
import { cn } from "@/lib/cn";

/** 의견 한 장 — 근거(본문)를 버튼보다 먼저 보여준다. 채택은 두 번 눌러 확정. */
function OpinionCard({ card }: { card: Card }) {
  const running = card.status === "running";
  const failed = card.status === "fail";
  const open = !card.verdict && !running && !failed;
  const role = AI_ROLE[card.who as AiName];

  return (
    <article
      aria-busy={running || undefined}
      className={cn(
        "flex min-w-0 flex-col gap-1.5 rounded-lg border border-border bg-raised px-3 py-2 transition-opacity duration-[var(--motion-fast)] ease-[var(--ease-out)]",
        card.verdict === "채택" && "border-adopt-line",
        card.verdict === "반려" && "opacity-45",
        failed && "border-dashed",
      )}
    >
      <div className="flex items-center gap-2 text-xs">
        <span className="font-semibold text-foreground">{card.who}</span>
        {role ? <span className="text-subtle">{role}</span> : null}
        <span className="ml-auto">
          {running ? <span className="text-subtle">응답 대기…</span> : null}
          {failed ? <span className="badge badge-reject">실패</span> : null}
          {card.verdict ? (
            <span className={cn("badge", card.verdict === "채택" ? "badge-adopt" : "badge-reject")}>{card.verdict}</span>
          ) : null}
        </span>
      </div>
      {running ? (
        <div className="space-y-1.5" aria-hidden>
          <div className="skeleton h-3.5 w-8/12" />
          <div className="skeleton h-3 w-11/12" />
          <div className="skeleton h-3 w-10/12" />
        </div>
      ) : (
        <>
          <h3 className={cn("text-sm font-semibold leading-snug", failed ? "text-muted" : "text-foreground")}>{card.title}</h3>
          {card.body ? (
            <p className="line-clamp-3 whitespace-pre-line text-xs leading-relaxed text-muted" title={card.body}>
              {card.body}
            </p>
          ) : null}
        </>
      )}
      {open ? (
        <div className="mt-auto pt-1">
          <DecideButtons id={card.id} compact />
        </div>
      ) : null}
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
  const open = list.filter((c) => !c.verdict && c.status !== "fail" && c.status !== "running");
  const ordered = OPINION_ROSTER.map((name) => list.find((c) => c.who === name)).filter((c): c is Card => Boolean(c));
  const extra = list.filter((c) => !OPINION_ROSTER.includes(c.who as (typeof OPINION_ROSTER)[number]));
  const glance = ordered.concat(extra);

  return (
    <section className="shrink-0 border-b border-border bg-elevated px-3 py-2" aria-labelledby="opinion-heading">
      <div className="mb-1.5 flex items-center gap-3">
        <h2 id="opinion-heading" className="text-sm font-semibold tracking-tight">
          의견
        </h2>
        <span className="font-mono text-xs tabular-nums text-muted">
          {open.length ? open.length + " 판정 대기" : "판정 대기 없음"}
        </span>
        {running ? <span className="text-xs text-subtle">CLI 응답 수집 중</span> : null}
        <button
          type="button"
          onClick={() => void gather()}
          disabled={busy || running || !hasCommand}
          className="btn-quiet ml-auto"
        >
          {running ? "수집 중…" : "의견 다시 받기"}
        </button>
      </div>
      {glance.length === 0 ? (
        <p className="empty">
          {hasCommand ? (running ? "CLI 응답을 기다리는 중" : "의견 없음 — 「의견 다시 받기」") : "명령을 내리면 CLI 4종이 의견을 낸다"}
        </p>
      ) : (
        <div className="grid grid-cols-2 gap-2 xl:grid-cols-4">
          {glance.map((card) => (
            <OpinionCard key={card.id} card={card} />
          ))}
        </div>
      )}
    </section>
  );
}
