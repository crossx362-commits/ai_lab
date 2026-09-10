import { createFileRoute } from "@tanstack/react-router";
import { useEffect } from "react";
import { CommandComposer } from "@/components/command-composer";
import { BoardColumns } from "@/components/board-columns";
import { ProjectRail } from "@/components/project-rail";
import { LabTree } from "@/components/lab-tree";
import { ProjectStatusCard } from "@/components/project-status";
import { OpinionLane } from "@/components/opinion-lane";
import { ProjectPanel } from "@/components/project-panel";
import { StatusLine } from "@/components/status-line";
import { useBoardStore } from "@/lib/board-store";
import { findLab } from "@/lib/lab-tree";
import { cn } from "@/lib/cn";

export const Route = createFileRoute("/")({ component: Home });

function Home() {
  const hydrate = useBoardStore((s) => s.hydrate);
  const cards = useBoardStore((s) => s.cards);
  const activeProjectId = useBoardStore((s) => s.activeProjectId);
  const panel = useBoardStore((s) => s.projectPanel);
  const togglePanel = useBoardStore((s) => s.toggleProjectPanel);
  const waiting = cards.filter((c) => c.col === "결정대기" && !c.verdict).length;
  const questions = cards.filter((c) => c.col === "질문" && !c.verdict).length;
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
          <p className="hidden min-w-0 truncate font-mono text-xs text-subtle xl:block">{node?.git ? node.git : "ai_lab"}</p>
          <div className="ml-auto flex shrink-0 items-center gap-2">
            <div className="hidden gap-3 text-xs text-muted sm:flex" aria-label="요약">
              <span>
                도장 기다림 <strong className={cn("text-foreground", waiting > 0 && "text-warn-fg")}>{waiting}</strong>
              </span>
              <span>
                질문 <strong className={cn("text-foreground", questions > 0 && "text-reject-fg")}>{questions}</strong>
              </span>
              <span>
                로봇 일하는 중 <strong className="text-foreground">{running}</strong>
              </span>
            </div>
            <button
              type="button"
              onClick={togglePanel}
              aria-expanded={panel}
              className={cn("btn-quiet", panel && "border-primary text-primary")}
            >
              프로젝트 현황
            </button>
            <a href="/eli5.html" target="_blank" rel="noreferrer" className="btn-quiet inline-flex items-center">
              이게 뭐예요?
            </a>
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
      <ProjectPanel />
      <ProjectStatusCard />
      <OpinionLane />
      <div className="flex min-h-0 flex-1 overflow-hidden">
        <LabTree />
        <BoardColumns />
      </div>
    </main>
  );
}
