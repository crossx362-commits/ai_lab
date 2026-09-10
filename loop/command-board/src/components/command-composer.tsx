import { useState } from "react";
import { ArrowUp, Copy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import { findLab } from "@/lib/lab-tree";
import { copyBriefing } from "@/lib/board-share";

export function CommandComposer() {
  const [value, setValue] = useState("");
  const [copied, setCopied] = useState(false);
  const addCommand = useBoardStore((s) => s.addCommand);
  const cards = useBoardStore((s) => s.cards);
  const active = useBoardStore((s) => s.activeProjectId);
  const node = findLab(active);

  function submit() {
    if (!value.trim()) return;
    addCommand(value);
    setValue("");
  }

  async function share() {
    await copyBriefing(useBoardStore.getState().cards);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  }

  return (
    <form
      className="flex min-w-0 items-center gap-2 rounded-xl border border-border bg-elevated p-1.5"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <label htmlFor="command-input" className="sr-only">
        명령
      </label>
      <input
        id="command-input"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder={(node?.name ?? "ai_lab") + " — 전원에게 시킬 일"}
        className="h-11 min-w-0 flex-1 bg-transparent px-3 text-base text-foreground outline-none placeholder:text-subtle"
      />
      <Button type="button" variant="secondary" size="touch" onClick={share} disabled={!cards.some((c) => c.col === "명령")}>
        <Copy className="size-4" />
        {copied ? "복사됨" : "전원 공유"}
      </Button>
      <Button type="submit" size="touch" disabled={!value.trim()}>
        내리기
        <ArrowUp className="size-4" />
      </Button>
    </form>
  );
}
