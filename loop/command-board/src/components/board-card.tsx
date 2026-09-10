import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import type { BoardCard as Card } from "@/lib/board-types";
import { findLab } from "@/lib/lab-tree";
import { cn } from "@/lib/cn";

export function BoardCard({ card, compact }: { card: Card; compact?: boolean }) {
  const decide = useBoardStore((s) => s.decide);
  const busy = useBoardStore((s) => s.busy);
  const latestCmd = useBoardStore((s) => s.cards.find((c) => c.col === "명령"));
  const running = card.status === "running";
  const failed = card.status === "fail";
  const yesNo = card.col === "결정대기" || card.col === "질문";
  const showBtns = (card.col === "의견" || yesNo) && !card.verdict && !running && !failed;
  const node = findLab(card.projectId || "");
  const live = card.col === "명령" && latestCmd?.id === card.id;

  return (
    <article
      className={cn(
        "shrink-0 rounded-lg border border-border bg-raised p-2.5",
        compact && "p-2",
        live && "border-primary",
        card.verdict === "채택" && "border-adopt/50",
        card.verdict === "반려" && "opacity-40",
        failed && "border-dashed opacity-60",
        card.done && "opacity-50",
      )}
    >
      <div className="flex items-center justify-between gap-2 text-xs text-subtle">
        <span className="truncate">
          {card.who}
          {node ? " · " + node.name : ""}
          {card.at ? " · " + card.at : ""}
        </span>
        {live ? (
          <span className="shrink-0 rounded-full bg-primary px-2 py-0.5 font-medium text-primary-foreground">전원</span>
        ) : null}
        {running ? <span className="shrink-0 animate-pulse text-subtle">응답 대기…</span> : null}
        {failed ? <span className="shrink-0 rounded-full bg-reject px-2 py-0.5 text-reject-fg">실패</span> : null}
        {card.done ? <span className="shrink-0 text-subtle">완료</span> : null}
        {card.verdict ? (
          <span
            className={cn(
              "shrink-0 rounded-full px-2 py-0.5 font-medium",
              card.verdict === "채택" && "bg-adopt text-adopt-fg",
              card.verdict === "반려" && "bg-reject text-reject-fg",
            )}
          >
            {yesNo ? (card.verdict === "채택" ? "예" : "아니오") : card.verdict}
          </span>
        ) : null}
      </div>
      <h3 className="mt-1.5 text-sm font-medium leading-snug text-foreground">{card.title}</h3>
      {card.body ? (
        <p className="mt-1 line-clamp-3 whitespace-pre-line text-xs leading-relaxed text-muted">{card.body}</p>
      ) : null}
      {showBtns ? (
        <div className="mt-3 grid grid-cols-2 gap-2">
          <Button type="button" variant="adopt" size="sm" disabled={busy} onClick={() => void decide(card.id, "채택")}>
            {yesNo ? "예" : "채택"}
          </Button>
          <Button type="button" variant="reject" size="sm" disabled={busy} onClick={() => void decide(card.id, "반려")}>
            {yesNo ? "아니오" : "반려"}
          </Button>
        </div>
      ) : null}
    </article>
  );
}
