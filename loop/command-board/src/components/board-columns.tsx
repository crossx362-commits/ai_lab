import { COLUMNS, type ColumnId } from "@/lib/board-types";
import { useBoardStore } from "@/lib/board-store";
import { BoardCard } from "@/components/board-card";
import { cn } from "@/lib/cn";

const HINTS: Record<ColumnId, string> = {
  명령: "여기",
  의견: "채택 / 반려",
  결정대기: "예 / 아니오",
  결정: "채택된 것만",
  실행: "진행",
  질문: "비움",
};

const VISIBLE = COLUMNS.filter((c) => c !== "의견");

export function BoardColumns() {
  const cards = useBoardStore((s) => s.cards);
  const move = useBoardStore((s) => s.move);

  return (
    <div className="grid h-full min-h-0 min-w-0 flex-1 grid-cols-5 grid-rows-1 gap-2 overflow-hidden p-2">
      {VISIBLE.map((col) => {
        const list = cards.filter((c) => c.col === col);
        return (
          <section
            key={col}
            className={cn(
              "flex h-full min-h-0 min-w-0 flex-col overflow-hidden rounded-xl border border-border bg-elevated",
              col === "질문" && list.length > 0 && "border-reject/50",
            )}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              const id = e.dataTransfer.getData("text/plain");
              if (id) move(id, col);
            }}
          >
            <header className="flex h-9 shrink-0 items-center justify-between px-3">
              <h2 className="text-sm font-medium tracking-tight text-foreground">{col}</h2>
              <span className="rounded-full bg-raised px-2 py-0.5 font-mono text-xs tabular-nums text-muted">
                {list.length}
              </span>
            </header>
            <div className="scroll-quiet flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto px-2 pb-2">
              {list.length === 0 ? (
                <p className="flex flex-1 items-center justify-center rounded-lg border border-dashed border-border px-2 text-center text-xs text-subtle">
                  {HINTS[col]}
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
