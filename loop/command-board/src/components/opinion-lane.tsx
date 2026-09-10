import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import { AI_ROSTER } from "@/lib/ai-roster";
import type { BoardCard as Card } from "@/lib/board-types";
import { cn } from "@/lib/cn";

function GlanceCard({ card }: { card: Card }) {
  const decide = useBoardStore((s) => s.decide);
  const open = !card.verdict;

  return (
    <article
      className={cn(
        "flex min-w-0 items-center gap-2 rounded-lg border border-border bg-raised px-2 py-1.5",
        card.verdict === "채택" && "border-adopt/50",
        card.verdict === "반려" && "opacity-40",
      )}
    >
      <div className="min-w-0 flex-1">
        <p className="text-xs text-subtle">{card.who}</p>
        <h3 className="truncate text-sm font-medium text-foreground">{card.title}</h3>
      </div>
      {open ? (
        <div className="flex shrink-0 gap-1">
          <Button type="button" variant="adopt" size="sm" onClick={() => decide(card.id, "채택")}>
            채택
          </Button>
          <Button type="button" variant="reject" size="sm" onClick={() => decide(card.id, "반려")}>
            반려
          </Button>
        </div>
      ) : (
        <span className="shrink-0 text-xs text-subtle">{card.verdict}</span>
      )}
    </article>
  );
}

export function OpinionLane() {
  const cards = useBoardStore((s) => s.cards);
  const gather = useBoardStore((s) => s.gatherOpinions);
  const move = useBoardStore((s) => s.move);
  const list = cards.filter((c) => c.col === "의견");
  const open = list.filter((c) => !c.verdict);
  const ordered = AI_ROSTER.map((name) => open.find((c) => c.who === name)).filter(
    (c): c is Card => Boolean(c),
  );
  const extra = open.filter((c) => !AI_ROSTER.includes(c.who as (typeof AI_ROSTER)[number]));
  const glance = ordered.concat(extra).slice(0, 5);

  return (
    <section
      className="shrink-0 border-b border-border bg-elevated px-3 py-1.5"
      onDragOver={(e) => e.preventDefault()}
      onDrop={(e) => {
        e.preventDefault();
        const id = e.dataTransfer.getData("text/plain");
        if (id) move(id, "의견");
      }}
    >
      <div className="mb-1.5 flex items-center gap-3">
        <h2 className="text-sm font-medium tracking-tight">의견</h2>
        <span className="font-mono text-xs tabular-nums text-muted">{open.length} 대기</span>
        <button
          type="button"
          onClick={gather}
          className="ml-auto h-8 rounded-full border border-border bg-raised px-3 text-xs text-foreground"
        >
          의견 받기
        </button>
      </div>
      {glance.length === 0 ? (
        <p className="rounded-lg border border-dashed border-border px-3 py-2 text-center text-sm text-subtle">
          비어 있음
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
