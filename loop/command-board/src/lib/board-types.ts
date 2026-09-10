export const COLUMNS = ["명령", "의견", "결정대기", "결정", "실행", "질문"] as const;
export type ColumnId = (typeof COLUMNS)[number];
export type Verdict = "채택" | "반려";

export type BoardCard = {
  id: string;
  col: ColumnId;
  who: string;
  title: string;
  body: string;
  projectId?: string;
  verdict?: Verdict;
  from?: string;
};

export const SAMPLE_CARDS: BoardCard[] = [
  {
    id: "c1",
    col: "명령",
    who: "나",
    projectId: "lab",
    title: "git 구조 연결",
    body: "D:\\ai_lab = crossx362-commits/ai_lab@master",
  },
  {
    id: "c2",
    col: "의견",
    who: "Grok",
    projectId: "petnna",
    title: "펫과나 기본 실행",
    body: "projects/petnna · index.html/js/api · Vercel 배포됨.",
  },
  {
    id: "c3",
    col: "의견",
    who: "GPT",
    projectId: "ai-team",
    title: "손은 ai-team/skills",
    body: "예원 영숙 봄이 수리 테오 백호 미오 나무 마루 별이.",
  },
  {
    id: "c4",
    col: "의견",
    who: "제미니",
    projectId: "ashes",
    title: "게임은 ashes-to-stars",
    body: "unity + art. 마루가 담당.",
  },
  {
    id: "c5",
    col: "의견",
    who: "Claude",
    projectId: "harness",
    title: "harness는 읽기전용",
    body: "check_all.py만. 하네스 수정 금지.",
  },
  {
    id: "c6",
    col: "의견",
    who: "Grok Build",
    projectId: "chinaguard",
    title: "C#은 루트 분리",
    body: "ChinaGuard · GeoGuard. 펫나 루프와 파일 안 겹침.",
  },
  {
    id: "c7",
    col: "결정",
    who: "나",
    projectId: "lab",
    title: "채택: 트리에서 대상 지정",
    body: "명령은 선택한 git 경로에만. 시크릿·배포·master force는 질문.",
  },
  {
    id: "c8",
    col: "실행",
    who: "보드",
    projectId: "lab",
    title: "트리 연결 완료",
    body: "projects 7 · 봇 10 · ChinaGuard · GeoGuard · docs · loop",
  },
];
