import { LAB_PROJECTS } from "@/lib/lab-projects";
import { PHASE_KO, statusOf } from "@/lib/lab-status";
import { useBoardStore } from "@/lib/board-store";
import { cn } from "@/lib/cn";

/** ai_lab이 제일 큰 집(대분류). 나머지는 전부 그 안의 방 — 집 칩 뒤에 「안에는」으로 잇는다. */
const HOME = "lab";
const RAIL = ["petnna", "ai-team", "ashes", "ulon", "geoguard"];

export function ProjectRail() {
  const active = useBoardStore((s) => s.activeProjectId);
  const setProject = useBoardStore((s) => s.setProject);
  const items = RAIL.map((id) => LAB_PROJECTS.find((p) => p.id === id)).filter(Boolean);
  const homeOn = active === HOME || !items.some((p) => p?.id === active);

  return (
    <div className="flex min-w-0 items-center gap-2 overflow-x-auto">
      <button
        type="button"
        onClick={() => setProject(HOME)}
        title="제일 큰 집 — 모든 프로젝트가 이 안에 있어요"
        className={cn(
          "h-8 shrink-0 rounded-full border-2 px-3 font-display text-sm transition-colors duration-[var(--motion-quick)] ease-[var(--ease-out)]",
          homeOn ? "border-ink bg-ink text-background" : "border-ink bg-raised text-foreground hover:bg-elevated",
        )}
      >
        🏠 ai_lab
        <span className={cn("ml-1.5 font-sans text-xs", homeOn ? "opacity-80" : "text-subtle")}>전체</span>
      </button>
      <span aria-hidden className="shrink-0 font-display text-sm text-subtle">
        안에는 →
      </span>
      {items.map((p) => {
        if (!p) return null;
        const st = statusOf(p.id);
        const on = active === p.id;
        return (
          <button
            key={p.id}
            type="button"
            onClick={() => setProject(p.id)}
            className={cn(
              "h-8 shrink-0 rounded-full border-2 px-3 font-display text-sm transition-colors duration-[var(--motion-quick)] ease-[var(--ease-out)]",
              on
                ? "border-sky bg-sky text-primary-foreground"
                : "border-border bg-raised text-muted hover:border-sky hover:text-foreground",
            )}
          >
            {p.name}
            <span className={cn("ml-1.5 font-sans text-xs", on ? "opacity-80" : "text-subtle")}>{PHASE_KO[st.phase]}</span>
          </button>
        );
      })}
    </div>
  );
}
