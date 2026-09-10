export const COLUMNS = ["명령", "의견", "결정대기", "결정", "실행", "질문"] as const;
export type ColumnId = (typeof COLUMNS)[number];
export type Verdict = "채택" | "반려";
export type OpinionStatus = "running" | "ok" | "fail" | "none";

/** 카드는 서버(`/api/board`)가 loop/BOARD.md와 loop/opinions/*.md에서 만든다. 화면은 지어내지 않는다. */
export type BoardCard = {
  id: string;
  col: ColumnId;
  who: string;
  title: string;
  body: string;
  projectId?: string;
  verdict?: Verdict;
  status?: OpinionStatus;
  done?: boolean;
  at?: string;
};
