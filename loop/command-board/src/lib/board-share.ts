import type { BoardCard } from "./board-types";
import { findLab } from "./lab-tree";
import { AI_ROSTER } from "./ai-roster";
import { OWNER_RULE } from "./owner-rules";

export const BOARD_URL = "https://github.com/crossx362-commits/ai_lab/blob/master/loop/BOARD.md";

export function briefing(cards: BoardCard[]) {
  const cmd = cards.find((c) => c.col === "명령");
  const node = findLab(cmd?.projectId || "");
  const opinions = cards.filter((c) => c.col === "의견" && !c.verdict).slice(0, 5);
  const decided = cards.find((c) => c.col === "결정");

  return [
    "# 지휘 보드 — 전원 필독",
    "",
    OWNER_RULE,
    "이 명령을 읽지 않았으면 일하지 않는다.",
    "Grok · GPT · 제미니 · Claude · Grok Build 전부 대상.",
    "의견만 낸다. 실행은 채택 후 Build 또는 Claude.",
    "",
    "원본: " + BOARD_URL,
    node ? "git: " + node.git : "",
    node ? "경로: " + node.win : "",
    "",
    "## 명령",
    cmd ? cmd.title : "(없음)",
    cmd?.body || "",
    "",
    "## 의견",
    opinions.length
      ? opinions.map((o) => "- " + o.who + ": " + o.title).join("\n")
      : "- (아직 없음)",
    "",
    "## 결정",
    decided ? decided.title : "- (채택 전 실행 금지)",
    "",
    "대상 AI: " + AI_ROSTER.join(" · "),
  ]
    .filter((line) => line !== "")
    .join("\n");
}

export async function copyBriefing(cards: BoardCard[]) {
  const text = briefing(cards);
  if (typeof navigator !== "undefined" && navigator.clipboard) {
    await navigator.clipboard.writeText(text);
  }
  return text;
}
