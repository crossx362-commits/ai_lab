import { LAB_PROJECTS } from "@/lib/lab-projects";
import { PHASE_KO, statusOf } from "@/lib/lab-status";
import { useBoardStore } from "@/lib/board-store";
import { cn } from "@/lib/cn";

const RAIL = ["petnna", "ai-team", "ashes", "ulon", "geoguard", "lab"];

export function ProjectRail() {
  const active = useBoardStore((s) => s.activeProjectId);
  const setProject = useBoardStore((s) => s.setProject);
  const items = RAIL.map((id) => LAB_PROJECTS.find((p) => p.id === id)).filter(Boolean);

  return (
    <div className="flex min-w-0 gap-2 overflow-x-auto">
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
              "h-8 shrink-0 rounded-full border px-3 text-xs transition-opacity duration-[var(--motion-quick)] ease-[var(--ease-out)]",
              on
                ? "border-primary bg-primary text-primary-foreground"
                : "border-border bg-raised text-muted hover:text-foreground",
            )}
          >
            {p.name}
            <span className={cn("ml-2 text-xs", on ? "opacity-70" : "text-subtle")}>{PHASE_KO[st.phase]}</span>
          </button>
        );
      })}
    </div>
  );
}
