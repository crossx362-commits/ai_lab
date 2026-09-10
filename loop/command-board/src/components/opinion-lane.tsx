import { useBoardStore } from "@/lib/board-store";
import { OPINION_ROSTER } from "@/lib/ai-roster";
import { COL, ROBOT, SAY } from "@/lib/words";
import { DecideButtons } from "@/components/decide-buttons";
import type { BoardCard as Card } from "@/lib/board-types";
import { cn } from "@/lib/cn";

/** AI마다 고정 색 — 누가 말했는지 색으로도 보이게(글자 이름은 항상 같이) */
const AVATAR: Record<string, string> = {
  Grok: "bg-primary text-primary-foreground",
  GPT: "bg-adopt-line text-white",
  제미니: "bg-lav-line text-white",
  Claude: "bg-warn-line text-foreground",
};

/** 의견 한 장 — 근거(본문)를 버튼보다 먼저 보여준다. 채택은 두 번 눌러 확정. */
function OpinionCard({ card }: { card: Card }) {
  const running = card.status === "running";
  const failed = card.status === "fail";
  const open = !card.verdict && !running && !failed;
  const robot = ROBOT[card.who as keyof typeof ROBOT];

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
        <span
          aria-hidden
          className={cn("inline-flex size-6 items-center justify-center rounded-full text-sm", AVATAR[card.who] || "bg-elevated text-muted")}
        >
          {robot?.emoji || card.who.slice(0, 1)}
        </span>
        <span className="font-semibold text-foreground">{card.who}</span>
        {robot ? <span className="text-subtle">{robot.role}</span> : null}
        <span className="ml-auto">
          {running ? <span className="text-subtle">{SAY.thinking}</span> : null}
          {failed ? <span className="badge badge-reject">{SAY.failed}</span> : null}
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
        <h2 id="opinion-heading" className="flex items-center gap-1.5 text-sm font-semibold tracking-tight" title={COL.의견.hint}>
          <span aria-hidden>{COL.의견.glyph}</span>
          {COL.의견.label}
        </h2>
        <span className="font-mono text-xs tabular-nums text-muted">
          {open.length ? open.length + "개 도장 기다려요" : "도장 기다리는 생각 없음"}
        </span>
        {running ? <span className="text-xs text-subtle">로봇들 {SAY.thinking}</span> : null}
        <button
          type="button"
          onClick={() => void gather()}
          disabled={busy || running || !hasCommand}
          className="btn-quiet ml-auto"
        >
          {running ? SAY.gathering : SAY.gather}
        </button>
      </div>
      {glance.length === 0 ? (
        <p className="empty">
          {hasCommand ? (running ? "로봇들이 생각 중… 🍵 (몇 분 걸려요)" : "생각이 없네요 — 「다시 생각해 봐!」") : COL.의견.empty}
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
