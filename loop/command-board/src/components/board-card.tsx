import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import type { BoardCard as Card } from "@/lib/board-types";
import { findLab } from "@/lib/lab-tree";
import { cn } from "@/lib/cn";

export function BoardCard({ card, compact }: { card: Card; compact?: boolean }) {
  const decide = useBoardStore((s) => s.decide);
  const latestCmd = useBoardStore((s) => s.cards.find((c) => c.col === "명령"));
  const showBtns = (card.col === "의견" || card.col === "결정대기") && !card.verdict;
  const node = findLab(card.projectId || "");
  const live = card.col === "명령" && latestCmd?.id === card.id;

  return (
    <article
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData("text/plain", card.id);
        e.dataTransfer.effectAllowed = "move";
      }}
      className={cn(
        "shrink-0 rounded-lg border border-border bg-raised p-2.5",
        compact && "p-2",
        live && "border-primary",
        card.verdict === "채택" && "border-adopt/50",
        card.verdict === "반려" && "opacity-40",
      )}
    >
      <div className="flex items-center justify-between gap-2 text-xs text-subtle">
        <span className="truncate">
          {card.who}
          {node ? " · " + node.name : ""}
        </span>
        {live ? (
          <span className="shrink-0 rounded-full bg-primary px-2 py-0.5 font-medium text-primary-foreground">전원</span>
        ) : null}
        {card.verdict ? (
          <span
            className={cn(
              "shrink-0 rounded-full px-2 py-0.5 font-medium",
              card.verdict === "채택" && "bg-adopt text-adopt-fg",
              card.verdict === "반려" && "bg-reject text-reject-fg",
            )}
          >
            {card.verdict}
          </span>
        ) : null}
      </div>
      <h3 className="mt-1.5 text-sm font-medium leading-snug text-foreground">{card.title}</h3>
      {card.body ? <p className="mt-1 line-clamp-2 text-xs leading-relaxed text-muted">{card.body}</p> : null}
      {showBtns ? (
        <div className="mt-3 grid grid-cols-2 gap-2">
          <Button type="button" variant="adopt" size="sm" onClick={() => decide(card.id, "채택")}>
            채택
          </Button>
          <Button type="button" variant="reject" size="sm" onClick={() => decide(card.id, "반려")}>
            반려
          </Button>
        </div>
      ) : null}
    </article>
  );
}
