import { useState } from "react";
import { ArrowUp, Copy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import { findLab } from "@/lib/lab-tree";
import { copyBriefing } from "@/lib/board-share";
import { SAY } from "@/lib/words";

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
      className="flex min-w-0 items-center gap-2 rounded-full border-2 border-border-strong bg-raised p-1 pl-3 shadow-[var(--shadow-sticker-lg)] transition-colors duration-[var(--motion-quick)] focus-within:border-sky"
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
        placeholder={(node && node.id !== "lab" ? "ai_lab › " + node.name : "ai_lab 전체") + " — " + SAY.placeholder}
        className="h-10 min-w-0 flex-1 bg-transparent px-2 text-[15px] text-foreground outline-none placeholder:text-subtle"
      />
      <Button type="button" variant="secondary" size="default" onClick={share} disabled={!cards.some((c) => c.col === "명령")} className="hidden sm:inline-flex">
        <Copy className="size-4" />
        {copied ? SAY.shared : SAY.share}
      </Button>
      <Button type="submit" size="default" disabled={!value.trim()} className="px-5">
        {SAY.send}
        <ArrowUp className="size-5" strokeWidth={2.5} />
      </Button>
    </form>
  );
}
