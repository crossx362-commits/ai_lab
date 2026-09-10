/**
 * 화면에 보이는 말(다섯 살 눈높이). BOARD.md의 절 이름(명령·의견·결정대기·결정·실행·질문)은 로봇들이 읽으니
 * 파일은 그대로 두고, 화면 라벨만 여기서 바꾼다. 말은 짧게, 그림은 하나씩.
 */
import type { ColumnId } from "./board-types";

export const COL = {
  명령: { label: "시킬 일", glyph: "📣", hint: "대장이 적은 말. 맨 위가 지금 것", empty: "아직 없어요 — 위에 한 줄 적으면 시작!" },
  의견: { label: "로봇 생각", glyph: "💭", hint: "로봇 넷이 각자 낸 생각", empty: "명령을 내리면 로봇 넷이 생각해서 와요" },
  결정대기: { label: "도장 기다림", glyph: "🤔", hint: "대장만 정할 수 있는 것. 예/아니오만", empty: "도장 기다리는 게 없어요 🎉" },
  결정: { label: "찍은 도장", glyph: "✅", hint: "대장이 찍은 도장의 역사", empty: "아직 찍은 도장이 없어요" },
  실행: { label: "로봇 할 일", glyph: "🛠️", hint: "☐ 아직 · ☑ 끝. 로봇이 스스로 채워요", empty: "로봇 할 일이 없어요 — 편하네요" },
  질문: { label: "막힌 것", glyph: "🙋", hint: "로봇이 막혀서 묻는 것. 비어 있으면 정상", empty: "비어 있음 = 다들 잘 알아들었어요" },
} as const satisfies Record<ColumnId, { label: string; glyph: string; hint: string; empty: string }>;

export const ROBOT = {
  Grok: { emoji: "🦊", role: "길잡이" },
  GPT: { emoji: "🐘", role: "설계" },
  제미니: { emoji: "🐬", role: "찾기" },
  Claude: { emoji: "🐻", role: "일꾼" },
  "Grok Build": { emoji: "🦫", role: "일꾼" },
} as const;

export const STATE = {
  진행: { emoji: "🟢", say: "로봇이 계속 일해요" },
  보류: { emoji: "🟡", say: "잠깐 쉬어요. 새 일은 안 해요" },
  완료: { emoji: "🏁", say: "다 했어요. 손대지 않아요" },
  접음: { emoji: "📦", say: "상자에 넣어 뒀어요" },
} as const;

export const SAY = {
  placeholder: "오늘 뭐 시킬까요? 한 줄이면 돼요",
  send: "시키기!",
  share: "복사해서 알리기",
  shared: "복사했어요",
  thinking: "생각 중… 🍵",
  gather: "다시 생각해 봐!",
  gathering: "생각 중…",
  adopt: "좋아, 해!",
  adoptConfirm: "진짜? 한 번 더!",
  reject: "아니야",
  yes: "응, 해",
  no: "아니",
  connected: "공책이랑 연결됨",
  offline: "공책이 안 보여요 — npm run dev",
  refresh: "다시 읽기",
  projects: "우리 프로젝트들",
  manual: "설명서",
  folders: "폴더들",
  failed: "못 했어요",
  done: "끝!",
  waitingStamp: "도장 기다림",
  questions: "막힌 것",
  working: "로봇 일하는 중",
} as const;
