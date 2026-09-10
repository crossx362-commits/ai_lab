import { useBoardStore } from "@/lib/board-store";
import { DecideButtons } from "@/components/decide-buttons";
import type { BoardCard as Card } from "@/lib/board-types";
import { findLab } from "@/lib/lab-tree";
import { cn } from "@/lib/cn";

export function BoardCard({ card, compact }: { card: Card; compact?: boolean }) {
  const latestCmd = useBoardStore((s) => s.cards.find((c) => c.col === "명령"));
  const running = card.status === "running";
  const failed = card.status === "fail";
  const yesNo = card.col === "결정대기" || card.col === "질문";
  const showBtns = (card.col === "의견" || yesNo) && !card.verdict && !running && !failed;
  const node = findLab(card.projectId || "");
  const live = card.col === "명령" && latestCmd?.id === card.id;
  const verdictLabel = card.verdict ? (yesNo ? (card.verdict === "채택" ? "예" : "아니오") : card.verdict) : "";

  return (
    <article
      aria-busy={running || undefined}
      className={cn(
        "shrink-0 rounded-lg border border-border bg-raised p-2.5 transition-opacity duration-[var(--motion-fast)] ease-[var(--ease-out)]",
        compact && "p-2",
        live && "border-primary",
        card.verdict === "채택" && "border-adopt-line",
        card.verdict === "반려" && "opacity-45",
        failed && "border-dashed opacity-70",
        card.done && "opacity-55",
      )}
    >
      <div className="flex items-center justify-between gap-2 text-xs text-subtle">
        <span className="truncate">
          {card.who}
          {node ? " · " + node.name : ""}
          {card.at ? " · " + card.at : ""}
        </span>
        {live ? <span className="badge badge-primary">전원</span> : null}
        {running ? <span className="text-subtle">응답 대기…</span> : null}
        {failed ? <span className="badge badge-reject">실패</span> : null}
        {card.done ? <span className="badge badge-mute">완료</span> : null}
        {verdictLabel ? (
          <span className={cn("badge", card.verdict === "채택" ? "badge-adopt" : "badge-reject")}>{verdictLabel}</span>
        ) : null}
      </div>
      <h3 className="mt-1.5 text-sm font-semibold leading-snug text-foreground">{card.title}</h3>
      {running ? (
        <div className="mt-2 space-y-1.5" aria-hidden>
          <div className="skeleton h-3 w-11/12" />
          <div className="skeleton h-3 w-9/12" />
          <div className="skeleton h-3 w-10/12" />
        </div>
      ) : card.body ? (
        <p className="mt-1 line-clamp-3 whitespace-pre-line text-xs leading-relaxed text-muted">{card.body}</p>
      ) : null}
      {showBtns ? <DecideButtons id={card.id} yesNo={yesNo} /> : null}
    </article>
  );
}
