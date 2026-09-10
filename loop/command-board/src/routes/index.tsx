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
import { StampInbox, pendingStamps } from "@/components/stamp-inbox";
import { Welcome } from "@/components/welcome";
import { Confetti } from "@/components/confetti";
import { SAY } from "@/lib/words";
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
  const waiting = pendingStamps(cards).length;
  const questions = cards.filter((c) => c.col === "질문" && !c.verdict).length;
  const running = cards.filter((c) => c.col === "실행" && !c.done).length;
  const node = findLab(activeProjectId);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return (
    <main className="flex h-dvh flex-col overflow-hidden bg-background text-foreground">
      <header className="shrink-0 border-b-2 border-border bg-elevated px-3 py-2">
        <div className="flex h-9 items-center gap-3">
          <h1 className="flex shrink-0 items-center gap-2 font-display text-[22px]" title={node?.git ? "ai_lab/" + node.git : "ai_lab (전체)"}>
            <span aria-hidden className="text-2xl leading-none">🎪</span>
            지휘 보드
          </h1>
          <div className="min-w-0 flex-1">
            <StatusLine />
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <div className="hidden gap-1.5 sm:flex" aria-label="요약">
              <span className={cn("badge", waiting > 0 ? "badge-warn" : "badge-mute")}>
                🤔 {SAY.waitingStamp} <strong>{waiting}</strong>
              </span>
              <span className={cn("badge", questions > 0 ? "badge-reject" : "badge-mute")}>
                🙋 {SAY.questions} <strong>{questions}</strong>
              </span>
              <span className={cn("badge", running > 0 ? "badge-lav" : "badge-mute")}>
                🛠️ {SAY.working} <strong>{running}</strong>
              </span>
            </div>
            <button
              type="button"
              onClick={togglePanel}
              aria-expanded={panel}
              className={cn("btn-quiet h-8 px-3 text-[13px]", panel && "border-sky bg-sky-soft")}
            >
              🧸 {SAY.projects}
            </button>
            <a href="/eli5.html" target="_blank" rel="noreferrer" className="btn-quiet h-8 px-3 text-[13px]">
              📖 {SAY.manual}
            </a>
          </div>
        </div>
        <div className="mt-1.5">
          <CommandComposer />
        </div>
        <div className="mt-1.5 flex min-w-0 items-center gap-3">
          <ProjectRail />
          <ProjectStatusCard />
        </div>
      </header>
      <Welcome />
      <Confetti />
      <ProjectPanel />
      <div className="flex shrink-0 flex-col xl:max-h-[34dvh] xl:flex-row xl:border-b-2 xl:border-border">
        <StampInbox />
        <OpinionLane />
      </div>
      <div className="flex min-h-0 flex-1 overflow-hidden">
        <LabTree />
        <BoardColumns />
      </div>
    </main>
  );
}
