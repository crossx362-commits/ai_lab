/**
 * 의견을 내는 AI 명단. 의견 본문은 여기서 만들지 않는다 —
 * `loop/dispatch-board.sh`가 실제 CLI를 불러 `loop/opinions/<AI>.md`에 남긴 것만 화면에 오른다.
 */
export const AI_ROSTER = ["Grok", "GPT", "제미니", "Claude", "Grok Build"] as const;
export type AiName = (typeof AI_ROSTER)[number];

export const AI_ROLE: Record<AiName, string> = {
  Grok: "방향",
  GPT: "구조",
  제미니: "조사",
  Claude: "실행",
  "Grok Build": "실행",
};
