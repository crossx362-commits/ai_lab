import { COLUMNS, type ColumnId } from "@/lib/board-types";
import { useBoardStore } from "@/lib/board-store";
import { BoardCard } from "@/components/board-card";
import { cn } from "@/lib/cn";
import { COL } from "@/lib/words";

/** 칸마다 제 색 — 어느 칸인지 색으로도, 그림으로도, 글자로도 */
const TINT: Record<ColumnId, { panel: string; head: string }> = {
  명령: { panel: "bg-sky-soft", head: "bg-sky text-white" },
  의견: { panel: "bg-lav-soft", head: "bg-lav text-white" },
  결정대기: { panel: "bg-lemon-soft", head: "bg-lemon text-on-bright" },
  결정: { panel: "bg-mint-soft", head: "bg-mint text-on-bright" },
  실행: { panel: "bg-lav-soft", head: "bg-lav text-white" },
  질문: { panel: "bg-coral-soft", head: "bg-coral text-white" },
};

/** 로봇 생각과 도장 기다림은 위쪽(로봇 생각 칸·받은편지함)에 있으니 아래 칸에서는 뺀다 */
const VISIBLE = COLUMNS.filter((c) => c !== "의견" && c !== "결정대기");

export function BoardColumns() {
  const cards = useBoardStore((s) => s.cards);

  return (
    <div className="scroll-quiet flex h-full min-h-0 min-w-0 flex-1 gap-3 overflow-x-auto overflow-y-hidden p-3">
      {VISIBLE.map((col) => {
        const list = cards.filter((c) => c.col === col);
        const tint = TINT[col];
        return (
          <section
            key={col}
            className={cn(
              "flex h-full min-h-0 w-[264px] shrink-0 flex-col overflow-hidden rounded-[var(--radius-xl)] border-2 border-border xl:w-auto xl:min-w-0 xl:flex-1",
              tint.panel,
            )}
          >
            <header className={cn("flex h-11 shrink-0 items-center justify-between px-3", tint.head)} title={COL[col].hint}>
              <h2 className="flex items-center gap-2 font-display text-[17px]">
                <span aria-hidden className="text-xl leading-none">
                  {COL[col].glyph}
                </span>
                {COL[col].label}
              </h2>
              <span className="inline-flex h-6 min-w-6 items-center justify-center rounded-full bg-white/85 px-2 font-mono text-xs font-semibold text-on-bright">
                {list.length}
              </span>
            </header>
            <div className="scroll-quiet flex min-h-0 flex-1 flex-col gap-2.5 overflow-y-auto p-2.5">
              {list.length === 0 ? <p className="empty flex-1">{COL[col].empty}</p> : list.map((card) => <BoardCard key={card.id} card={card} />)}
            </div>
          </section>
        );
      })}
    </div>
  );
}
