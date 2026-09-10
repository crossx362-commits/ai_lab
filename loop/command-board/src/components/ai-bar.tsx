import { AI_ROSTER, AI_ROLE } from "@/lib/ai-roster";
import { useBoardStore } from "@/lib/board-store";

export function AiBar() {
  const gather = useBoardStore((s) => s.gatherOpinions);

  return (
    <div className="flex min-w-0 items-center gap-2 overflow-x-auto">
      {AI_ROSTER.map((name) => (
        <span
          key={name}
          className="h-10 shrink-0 rounded-full border border-border bg-raised px-3 text-sm leading-10 text-foreground"
        >
          {name}
          <span className="ml-1.5 text-xs text-subtle">{AI_ROLE[name]}</span>
        </span>
      ))}
      <button
        type="button"
        onClick={gather}
        className="h-10 shrink-0 rounded-full border border-border bg-elevated px-4 text-sm text-foreground hover:bg-raised"
      >
        의견 받기
      </button>
    </div>
  );
}
