import { useState } from "react";
import { useBoardStore } from "@/lib/board-store";
import { OPINION_ROSTER } from "@/lib/ai-roster";
import { COL, ROBOT, SAY } from "@/lib/words";
import { DecideButtons } from "@/components/decide-buttons";
import type { BoardCard as Card } from "@/lib/board-types";
import { cn } from "@/lib/cn";

/** AI마다 고정 색 — 누가 말했는지 색으로도 보이게(글자 이름은 항상 같이) */
const AVATAR: Record<string, string> = {
  Grok: "bg-sky-soft",
  GPT: "bg-mint-soft",
  제미니: "bg-lav-soft",
  Claude: "bg-lemon-soft",
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
        "sticker flex min-w-0 flex-col gap-1 px-2.5 py-2",
        card.verdict === "채택" && "border-mint",
        card.verdict === "반려" && "opacity-45",
        failed && "border-dashed",
      )}
    >
      <div className="flex min-w-0 items-center gap-1.5 text-xs">
        <span
          aria-hidden
          className={cn("inline-flex size-6 shrink-0 items-center justify-center rounded-full text-sm", AVATAR[card.who] || "bg-elevated text-muted", running && "wobble")}
        >
          {robot?.emoji || card.who.slice(0, 1)}
        </span>
        <span className="min-w-0 truncate font-display text-sm text-foreground">{card.who}</span>
        {robot ? <span className="badge badge-mute hidden xl:inline-flex">{robot.role}</span> : null}
        <span className="ml-auto shrink-0">
          {running ? <span className="text-subtle">{SAY.thinking}</span> : null}
          {failed ? <span className="badge badge-reject">{SAY.failed}</span> : null}
          {card.verdict ? (
            <span className={cn("badge", card.verdict === "채택" ? "badge-adopt" : "badge-reject")}>{card.verdict === "채택" ? "✅ 좋아!" : "❌ " + SAY.reject}</span>
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
          <h3 className={cn("line-clamp-1 text-sm font-semibold leading-snug", failed ? "text-muted" : "text-foreground")} title={card.title}>{card.title}</h3>
          {card.body ? (
            <p className="line-clamp-2 whitespace-pre-line text-xs leading-relaxed text-muted" title={card.body}>
              {card.body}
            </p>
          ) : null}
        </>
      )}
      {open ? (
        <div className="mt-auto pt-0.5">
          <DecideButtons id={card.id} compact />
        </div>
      ) : null}
    </article>
  );
}

const OPEN_KEY = "command-board:lane-open";

function readOpen() {
  try {
    return window.localStorage.getItem(OPEN_KEY) !== "0";
  } catch {
    return true;
  }
}

export function OpinionLane() {
  const [expanded, setExpanded] = useState(readOpen);
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
  const toggle = () => {
    const next = !expanded;
    setExpanded(next);
    try {
      window.localStorage.setItem(OPEN_KEY, next ? "1" : "0");
    } catch {
      /* 저장 못 해도 이번 화면은 바뀐다 */
    }
  };

  return (
    <section className="shrink-0 border-b-2 border-border bg-lav-soft/60 px-3 py-2" aria-labelledby="opinion-heading">
      <div className="mb-1.5 flex items-center gap-3">
        <h2 id="opinion-heading" className="flex items-center gap-1.5 font-display text-[15px]" title={COL.의견.hint}>
          <span aria-hidden className="text-lg leading-none">{COL.의견.glyph}</span>
          {COL.의견.label}
        </h2>
        <span className="text-[13px] tabular-nums text-muted">
          {open.length ? open.length + "개 도장 기다려요" : "도장 기다리는 생각 없음"}
        </span>
        {!expanded ? (
          <span className="flex items-center gap-1.5" aria-label="로봇별 상태">
            {glance.map((c) => (
              <span key={c.id} className="badge badge-mute" title={c.title}>
                {ROBOT[c.who as keyof typeof ROBOT]?.emoji || c.who.slice(0, 1)}
                {c.status === "running" ? " …" : c.status === "fail" ? " 😵" : c.verdict === "채택" ? " ✅" : c.verdict === "반려" ? " ❌" : " 💭"}
              </span>
            ))}
          </span>
        ) : null}
        {running ? <span className="text-[13px] text-subtle"><span className="wobble" aria-hidden>🤖</span> 로봇들 {SAY.thinking}</span> : null}
        <button
          type="button"
          onClick={() => void gather()}
          disabled={busy || running || !hasCommand}
          className="btn-quiet ml-auto"
        >
          {running ? SAY.gathering : SAY.gather}
        </button>
        <button type="button" onClick={toggle} aria-expanded={expanded} className="btn-quiet" title={expanded ? "로봇 생각 접기" : "로봇 생각 펼치기"}>
          {expanded ? "접기 ▴" : "펼치기 ▾"}
        </button>
      </div>
      {!expanded ? null : glance.length === 0 ? (
        <p className="empty">
          {hasCommand ? (running ? "로봇들이 생각 중… 🍵 (몇 분 걸려요)" : "생각이 없네요 — 「다시 생각해 봐!」") : COL.의견.empty}
        </p>
      ) : (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {glance.map((card) => (
            <OpinionCard key={card.id} card={card} />
          ))}
        </div>
      )}
    </section>
  );
}
