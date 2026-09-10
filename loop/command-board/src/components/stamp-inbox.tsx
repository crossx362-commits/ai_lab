import { useEffect, useState } from "react";
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

function typing(e: KeyboardEvent) {
  const el = e.target as HTMLElement | null;
  return Boolean(el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA" || el.isContentEditable));
}

/**
 * 도장 받은편지함 — 맨 위 레몬 상자. 한 줄에 하나, 바로 도장.
 * 키보드(GitHub PR 대시보드·Linear 분류함식): j/k 이동 · a 좋아(두 번) · r 아니야 · ? 도움말 · Esc 취소.
 */
export function StampInbox() {
  const cards = useBoardStore((s) => s.cards);
  const decide = useBoardStore((s) => s.decide);
  const busy = useBoardStore((s) => s.busy);
  const list = pendingStamps(cards);
  const [focus, setFocus] = useState(0);
  const [armId, setArmId] = useState("");
  const [help, setHelp] = useState(false);
  const cur = list[Math.min(focus, list.length - 1)];

  useEffect(() => {
    if (!armId) return;
    const t = window.setTimeout(() => setArmId(""), 3000);
    return () => window.clearTimeout(t);
  }, [armId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (typing(e) || e.metaKey || e.ctrlKey || e.altKey) return;
      if (e.key === "?") {
        setHelp((v) => !v);
        return;
      }
      if (e.key === "Escape") {
        setArmId("");
        setHelp(false);
        return;
      }
      if (!list.length) return;
      if (e.key === "j" || e.key === "ArrowDown") {
        e.preventDefault();
        setFocus((i) => Math.min(i + 1, list.length - 1));
        setArmId("");
      } else if (e.key === "k" || e.key === "ArrowUp") {
        e.preventDefault();
        setFocus((i) => Math.max(i - 1, 0));
        setArmId("");
      } else if (e.key === "a" && cur && !busy) {
        if (armId === cur.id) {
          setArmId("");
          void decide(cur.id, "채택");
        } else {
          setArmId(cur.id);
        }
      } else if (e.key === "r" && cur && !busy) {
        setArmId("");
        void decide(cur.id, "반려");
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [list, cur, armId, busy, decide]);

  useEffect(() => {
    document.querySelector<HTMLElement>('[data-inbox-row="focus"]')?.scrollIntoView({ block: "nearest" });
  }, [focus, list.length]);

  if (list.length === 0) {
    return (
      <p className="shrink-0 border-b-2 border-border bg-elevated px-3 py-1.5 text-[13px] text-subtle xl:w-[56%] xl:border-b-0 xl:border-r-2">
        🎉 도장 기다리는 게 없어요 — 편하네요
      </p>
    );
  }

  return (
    <section
      aria-labelledby="inbox-heading"
      className="relative flex min-h-0 shrink-0 flex-col border-b-2 border-lemon bg-lemon-soft px-3 py-1.5 xl:w-[56%] xl:shrink xl:border-b-0 xl:border-r-2"
    >
      <div className="mb-1 flex items-center gap-2">
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
        <button
          type="button"
          onClick={() => setHelp((v) => !v)}
          aria-expanded={help}
          className="ml-auto hidden font-mono text-xs text-subtle hover:text-foreground md:inline"
          title="키보드로도 돼요 — ? 를 누르면 도움말"
        >
          j/k 이동 · a 좋아 · r 아니야 · ?
        </button>
      </div>
      {help ? (
        <div role="dialog" aria-label="키보드 도움말" className="sticker absolute right-3 top-9 z-10 w-64 p-3 text-[13px]">
          <p className="font-display text-base">⌨️ 손가락 세 개면 돼요</p>
          <ul className="mt-1.5 space-y-1 text-muted">
            <li>
              <kbd className="rounded border px-1 font-mono">j</kbd> / <kbd className="rounded border px-1 font-mono">k</kbd> 아래·위로
            </li>
            <li>
              <kbd className="rounded border px-1 font-mono">a</kbd> 좋아 — 두 번 누르면 확정
            </li>
            <li>
              <kbd className="rounded border px-1 font-mono">r</kbd> 아니야 — 한 번
            </li>
            <li>
              <kbd className="rounded border px-1 font-mono">Esc</kbd> 취소 · <kbd className="rounded border px-1 font-mono">?</kbd> 이 창
            </li>
          </ul>
        </div>
      ) : null}
      <ul
        className="scroll-quiet flex max-h-[22dvh] min-h-0 flex-col gap-1 overflow-y-auto pr-1 xl:max-h-none xl:flex-1"
        role="listbox"
        aria-activedescendant={cur ? "inbox-" + cur.id : undefined}
      >
        {list.map((c) => {
          const yesNo = c.col !== "의견";
          const robot = ROBOT[c.who as keyof typeof ROBOT];
          const kind = KIND[c.col];
          const on = cur?.id === c.id;
          return (
            <li
              key={c.id}
              id={"inbox-" + c.id}
              role="option"
              aria-selected={on}
              data-inbox-row={on ? "focus" : undefined}
              onMouseEnter={() => setFocus(list.indexOf(c))}
              className={cn("sticker flex items-center gap-2.5 px-3 py-1 hover:transform-none", on && "border-sky")}
            >
              <span className={cn("badge", kind.cls)}>{kind.tag}</span>
              <span className="shrink-0 text-[13px] text-subtle">
                {robot ? robot.emoji + " " : ""}
                {c.who}
              </span>
              <span className="min-w-0 flex-1 truncate text-[15px] font-semibold text-foreground" title={c.body ? c.title + "\n\n" + c.body : c.title}>
                {c.title}
              </span>
              <DecideButtons id={c.id} yesNo={yesNo} compact forceArm={armId === c.id} onArmChange={(v) => setArmId(v ? c.id : "")} />
            </li>
          );
        })}
      </ul>
    </section>
  );
}
