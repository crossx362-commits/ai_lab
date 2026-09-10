import { useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { LAB_TREE, type LabNode } from "@/lib/lab-tree";
import { useBoardStore } from "@/lib/board-store";
import { cn } from "@/lib/cn";

function Branch({ node, depth }: { node: LabNode; depth: number }) {
  const active = useBoardStore((s) => s.activeProjectId);
  const setProject = useBoardStore((s) => s.setProject);
  const kids = node.children ?? [];
  const [open, setOpen] = useState(depth < 1);

  return (
    <div className={depth ? "ml-3 border-l border-border pl-1" : ""}>
      <div className="flex items-center">
        {kids.length ? (
          <button
            type="button"
            className="flex h-8 w-8 shrink-0 items-center justify-center text-subtle"
            onClick={() => setOpen((v) => !v)}
            aria-label={open ? "접기" : "펼치기"}
          >
            {open ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
          </button>
        ) : (
          <span className="inline-block w-8" />
        )}
        <button
          type="button"
          onClick={() => setProject(node.id)}
          className={cn(
            "min-w-0 flex-1 truncate rounded-md px-2 py-1.5 text-left text-sm transition-colors duration-[var(--motion-quick)] ease-[var(--ease-out)]",
            active === node.id
              ? "bg-primary text-primary-foreground"
              : "text-muted hover:bg-raised hover:text-foreground",
          )}
        >
          {node.name}
        </button>
      </div>
      {open ? kids.map((child) => <Branch key={child.id} node={child} depth={depth + 1} />) : null}
    </div>
  );
}

export function LabTree() {
  return (
    <aside className="hidden min-h-0 w-48 shrink-0 flex-col overflow-hidden border-r border-border bg-elevated xl:flex">
      <p className="shrink-0 px-4 py-3 text-xs tracking-wide text-subtle">저장소</p>
      <div className="scroll-quiet min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        <Branch node={LAB_TREE} depth={0} />
      </div>
    </aside>
  );
}
