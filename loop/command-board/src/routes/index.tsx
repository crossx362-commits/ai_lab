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
import { StatusLine } from "@/components/status-line";

export const Route = createFileRoute("/")({ component: Home });

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
      <header className="shrink-0 border-b border-border bg-elevated px-3 py-2">
        <div className="flex items-center gap-3">
          <h1 className="shrink-0 font-display text-xl font-semibold tracking-[-0.02em]">지휘 보드</h1>
          <p className="hidden min-w-0 truncate font-mono text-xs text-subtle xl:block">
            {node?.git ? node.git : "ai_lab"}
          </p>
          <div className="ml-auto flex shrink-0 gap-3 text-xs text-muted" aria-label="요약">
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
