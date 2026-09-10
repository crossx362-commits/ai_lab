import { statusOf } from "./lab-status";
import { findLab } from "./lab-tree";
import { OWNER_RULE } from "./owner-rules";
import type { BoardCard } from "./board-types";

export const AI_ROSTER = ["Grok", "GPT", "제미니", "Claude", "Grok Build"] as const;
export type AiName = (typeof AI_ROSTER)[number];

export const AI_ROLE: Record<AiName, string> = {
  Grok: "방향",
  GPT: "구조",
  제미니: "조사",
  Claude: "실행",
  "Grok Build": "실행",
};

export function draftOpinions(projectId: string, command: string): Omit<BoardCard, "id">[] {
  const st = statusOf(projectId);
  const node = findLab(projectId);
  const where = node?.git || "ai_lab";
  const topic = command.slice(0, 40);

  return [
    {
      col: "의견",
      who: "Grok",
      projectId,
      title: "범위는 " + where,
      body: topic + "\n" + st.now + "\n" + OWNER_RULE,
    },
    {
      col: "의견",
      who: "GPT",
      projectId,
      title: "파일 경계 유지",
      body: where + " 밖은 손대지 않음. 오너 손 필요하면 그 안은 버린다.",
    },
    {
      col: "의견",
      who: "제미니",
      projectId,
      title: "문서 먼저",
      body: (st.docs[0]?.git || "README") + " 기준으로만. 추측으로 상태 안 올림.",
    },
    {
      col: "의견",
      who: "Claude",
      projectId,
      title: "채택 후 실행",
      body: "채택되면 " + where + " 자율 루프. 오너에게 확인·설치·실행을 시키지 않음.",
    },
    {
      col: "의견",
      who: "Grok Build",
      projectId,
      title: "병렬은 경로 분리",
      body: "같은 파일 안 겹치게 나눔. 배포·시크릿은 멈춤. 오너 할 일 목록 만들지 않음.",
    },
  ];
}
