import { useBoardStore } from "@/lib/board-store";
import { DecideButtons } from "@/components/decide-buttons";
import type { BoardCard } from "@/lib/board-types";
import { ROBOT } from "@/lib/words";
import { cn } from "@/lib/cn";

/** 대장 도장을 기다리는 것 전부 — 결정대기·막힌 것·로봇 생각(아직 판정 전). 이 순서로. */
export function pendingStamps(cards: BoardCard[]) {
  const open = cards.filter((c) => !c.verdict && c.status !== "running" && c.status !== "fail");
  const rank: Record<string, number> = { 결정대기: 0, 질문: 1, 의견: 2 };
  return open.filter((c) => c.col in rank).sort((a, b) => rank[a.col] - rank[b.col]);
}

const KIND: Record<string, { tag: string; cls: string }> = {
  결정대기: { tag: "🤔 예/아니오", cls: "badge-warn" },
  질문: { tag: "🙋 막힘", cls: "badge-reject" },
  의견: { tag: "💭 생각", cls: "badge-lav" },
};

/** 도장 받은편지함 — 맨 위 레몬색 상자. 한 줄에 하나, 바로 도장. 비면 작게 「없어요 🎉」. */
export function StampInbox() {
  const cards = useBoardStore((s) => s.cards);
  const list = pendingStamps(cards);

  if (list.length === 0) {
    return (
      <p className="shrink-0 border-b-2 border-border bg-elevated px-3 py-1.5 text-[13px] text-subtle">
        🎉 도장 기다리는 게 없어요 — 편하네요
      </p>
    );
  }

  return (
    <section
      aria-labelledby="inbox-heading"
      className="shrink-0 border-b-2 border-lemon bg-lemon-soft px-3 py-2"
    >
      <div className="mb-1.5 flex items-center gap-2">
        <h2 id="inbox-heading" className="flex items-center gap-2 font-display text-[17px]">
          <span aria-hidden className="text-xl leading-none">
            🤔
          </span>
          도장 기다림
          <span className="inline-flex h-6 min-w-6 items-center justify-center rounded-full bg-lemon px-2 font-mono text-sm font-bold text-on-bright">
            {list.length}
          </span>
        </h2>
        <span className="text-[13px] text-muted">대장이 찍어야 로봇이 움직여요</span>
      </div>
      <ul className="scroll-quiet flex max-h-[26dvh] flex-col gap-1.5 overflow-y-auto pr-1">
        {list.map((c) => {
          const yesNo = c.col !== "의견";
          const robot = ROBOT[c.who as keyof typeof ROBOT];
          const kind = KIND[c.col];
          return (
            <li key={c.id} className="sticker flex items-center gap-2.5 px-3 py-1.5 hover:transform-none">
              <span className={cn("badge", kind.cls)}>{kind.tag}</span>
              <span className="shrink-0 text-[13px] text-subtle">
                {robot ? robot.emoji + " " : ""}
                {c.who}
              </span>
              <span className="min-w-0 flex-1 truncate text-[15px] font-semibold text-foreground" title={c.body ? c.title + "\n\n" + c.body : c.title}>
                {c.title}
              </span>
              <DecideButtons id={c.id} yesNo={yesNo} compact />
            </li>
          );
        })}
      </ul>
    </section>
  );
}
