import { createFileRoute } from "@tanstack/react-router";
import { useEffect } from "react";
import { CommandComposer } from "@/components/command-composer";
import { BoardColumns } from "@/components/board-columns";
import { ProjectRail } from "@/components/project-rail";
import { LabTree } from "@/components/lab-tree";
import { ProjectStatusCard } from "@/components/project-status";
import { OpinionLane } from "@/components/opinion-lane";
import { useBoardStore } from "@/lib/board-store";
import { findLab } from "@/lib/lab-tree";
import { cn } from "@/lib/cn";

export const Route = createFileRoute("/")({ component: Home });

const CLI = ["claude", "codex", "gemini", "grok"] as const;

function StatusLine() {
  const online = useBoardStore((s) => s.online);
  const ready = useBoardStore((s) => s.ready);
  const head = useBoardStore((s) => s.head);
  const error = useBoardStore((s) => s.error);
  const notice = useBoardStore((s) => s.notice);
  const tools = useBoardStore((s) => s.tools);

  if (!ready) return <p className="text-xs text-subtle">보드 읽는 중…</p>;
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-xs">
      <span className={cn("rounded-full px-2 py-0.5", online ? "bg-adopt text-adopt-fg" : "bg-reject text-reject-fg")}>
        {online ? "BOARD.md 연결 · " + head : "서버 없음 — npm run dev"}
      </span>
      {online
        ? CLI.map((b) => (
            <span key={b} className={cn("font-mono", tools[b] ? "text-muted" : "text-reject-fg line-through")}>
              {b}
              {b === "grok" && tools.grok && !tools.grokLogin ? " (로그인 전)" : ""}
            </span>
          ))
        : null}
      {error ? <span className="text-reject-fg">{error}</span> : null}
      {notice && !error ? <span className="text-muted">{notice}</span> : null}
    </div>
  );
}

function Home() {
  const hydrate = useBoardStore((s) => s.hydrate);
  const cards = useBoardStore((s) => s.cards);
  const activeProjectId = useBoardStore((s) => s.activeProjectId);
  const waiting = cards.filter((c) => c.col === "결정대기" && !c.verdict).length;
  const questions = cards.filter((c) => c.col === "질문").length;
  const running = cards.filter((c) => c.col === "실행" && !c.done).length;
  const node = findLab(activeProjectId);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return (
    <main className="flex h-dvh flex-col overflow-hidden bg-background text-foreground">
      <header className="shrink-0 border-b border-border px-3 py-2">
        <div className="flex items-center gap-3">
          <h1 className="shrink-0 font-display text-lg tracking-tight">지휘 보드</h1>
          <p className="hidden min-w-0 truncate font-mono text-xs text-subtle xl:block">
            {node?.git ? node.git : "ai_lab"}
          </p>
          <div className="ml-auto flex shrink-0 gap-3 font-mono text-xs tabular-nums text-muted">
            <span>
              대기 <strong className="text-foreground">{waiting}</strong>
            </span>
            <span>
              질문 <strong className="text-foreground">{questions}</strong>
            </span>
            <span>
              실행 <strong className="text-foreground">{running}</strong>
            </span>
          </div>
        </div>
        <div className="mt-1.5">
          <StatusLine />
        </div>
        <div className="mt-2">
          <CommandComposer />
        </div>
        <div className="mt-2">
          <ProjectRail />
        </div>
      </header>
      <ProjectStatusCard />
      <OpinionLane />
      <div className="flex min-h-0 flex-1 overflow-hidden">
        <LabTree />
        <BoardColumns />
      </div>
    </main>
  );
}
