import { COLUMNS, type ColumnId } from "@/lib/board-types";
import { useBoardStore } from "@/lib/board-store";
import { BoardCard } from "@/components/board-card";
import { cn } from "@/lib/cn";
import { COL } from "@/lib/words";


/** 열마다 다른 색 띠 — 어느 칸인지 색으로도, 글자로도 */
const TINT: Record<ColumnId, string> = {
  명령: "bg-primary",
  의견: "bg-primary",
  결정대기: "bg-warn-line",
  결정: "bg-adopt-line",
  실행: "bg-lav-line",
  질문: "bg-reject-line",
};

const VISIBLE = COLUMNS.filter((c) => c !== "의견");

export function BoardColumns() {
  const cards = useBoardStore((s) => s.cards);

  return (
    <div className="scroll-quiet flex h-full min-h-0 min-w-0 flex-1 gap-2 overflow-x-auto overflow-y-hidden p-2">
      {VISIBLE.map((col) => {
        const list = cards.filter((c) => c.col === col);
        return (
          <section
            key={col}
            className={cn(
              "flex h-full min-h-0 w-[248px] shrink-0 flex-col overflow-hidden rounded-xl border border-border bg-elevated xl:w-auto xl:min-w-0 xl:flex-1",
              col === "질문" && list.length > 0 && "border-reject/50",
            )}
          >
            <div aria-hidden className={cn("h-1 w-full shrink-0", TINT[col])} />
            <header className="flex h-9 shrink-0 items-center justify-between px-3">
              <h2 className="flex items-center gap-1.5 text-[13px] font-semibold text-foreground" title={COL[col].hint}>
                <span aria-hidden>{COL[col].glyph}</span>
                {COL[col].label}
              </h2>
              <span className="rounded-sm bg-raised px-1.5 py-0.5 font-mono text-[11px] text-subtle">
                {list.length}
              </span>
            </header>
            <div className="scroll-quiet flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto px-2 pb-2">
              {list.length === 0 ? (
                <p className="empty flex-1">
                  {COL[col].empty}
                </p>
              ) : (
                list.map((card) => <BoardCard key={card.id} card={card} />)
              )}
            </div>
          </section>
        );
      })}
    </div>
  );
}
