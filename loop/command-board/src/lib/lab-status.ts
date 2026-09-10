export type DocRef = { title: string; git: string };
export type ProjectStatus = {
  id: string;
  phase: "live" | "loop" | "review" | "idle" | "docs";
  label: string;
  updated: string;
  now: string;
  next: string;
  docs: DocRef[];
};

export const LAB_DOCS: DocRef[] = [
  { title: "AGENTS.md", git: "AGENTS.md" },
  { title: "CLAUDE.md", git: "CLAUDE.md" },
  { title: "DIRECTIVES.md", git: "DIRECTIVES.md" },
  { title: "HANDBOOK.md", git: "HANDBOOK.md" },
  { title: "PROJECT_OVERVIEW.md", git: "PROJECT_OVERVIEW.md" },
  { title: "재와별 STATUS", git: "docs/STATUS.md" },
  { title: "Ulon 인수인계", git: "docs/SESSION_HANDOFF.md" },
  { title: "문서 분류", git: "docs/REPOSITORY_CLASSIFICATION.md" },
];

export const PROJECT_STATUS: Record<string, ProjectStatus> = {
  lab: {
    id: "lab",
    phase: "docs",
    label: "모노레포",
    updated: "2026-09-10",
    now: "master HEAD e29379c. 지휘 보드 loop/BOARD.md.",
    next: "시크릿·배포는 멈춤. 오너에게 시키지 않음.",
    docs: LAB_DOCS.slice(0, 5),
  },
  petnna: {
    id: "petnna",
    phase: "live",
    label: "라이브 PWA",
    updated: "2026-08-16",
    now: "git 마지막 2026-08-16. CHANGELOG v1.3.0 (2026-06-11) petnna.vercel.app.",
    next: "봄이 QA → 수리. 병합은 AI가 브라우저에서 확인.",
    docs: [
      { title: "README", git: "projects/petnna/README.md" },
      { title: "CHANGELOG", git: "projects/petnna/CHANGELOG.md" },
      { title: "Supabase", git: "projects/petnna/SETUP_SUPABASE.md" },
    ],
  },
  "ai-team": {
    id: "ai-team",
    phase: "live",
    label: "운영 중",
    updated: "2026-09-03",
    now: "텔레그램·스케줄 영구 비활성. 예원·영숙 데몬은 맥.",
    next: "harness/check_all.py. 데몬은 맥만.",
    docs: [
      { title: "README", git: "projects/ai-team/README.md" },
      { title: "CLAUDE.md", git: "CLAUDE.md" },
    ],
  },
  ashes: {
    id: "ashes",
    phase: "loop",
    label: "자율 루프",
    updated: "2026-08-20",
    now: "캐릭터 속성 패널 밀도 노출. 마지막 트랙=코드.",
    next: "폴리싱 다음 = 파티. 그다음 월드맵→던전.",
    docs: [
      { title: "STATUS", git: "docs/STATUS.md" },
      { title: "README", git: "projects/ashes-to-stars/README.md" },
      { title: "원장", git: "docs/GAME_DESIGN_ASHES_TO_STARS.md" },
    ],
  },
  ulon: {
    id: "ulon",
    phase: "review",
    label: "검수 루프",
    updated: "2026-09-09",
    now: "수면 되풀이 도장 랩. Grok 차선 B 닫힘. HUD 재작성.",
    next: "다음 큐 A: Selected/Active* → WorldBody.",
    docs: [
      { title: "README", git: "projects/ulon/README.md" },
      { title: "인수인계", git: "docs/SESSION_HANDOFF.md" },
      { title: "기획", git: "projects/ulon/docs/GAME_DESIGN.md" },
    ],
  },
  homepage: {
    id: "homepage",
    phase: "live",
    label: "사이트",
    updated: "2026-08-20",
    now: "코드 검토 10건 반영. 블로그·방명록 라이브.",
    next: "콘텐츠만. 엔진 루프와 섞지 않음.",
    docs: [{ title: "index", git: "projects/homepage/index.html" }],
  },
  chinaguard: {
    id: "chinaguard",
    phase: "idle",
    label: "구버전",
    updated: "2026-08-19",
    now: "중국 IP 차단 트레이. GeoGuard가 후속.",
    next: "신규는 GeoGuard.",
    docs: [{ title: "README", git: "ChinaGuard/README.md" }],
  },
  geoguard: {
    id: "geoguard",
    phase: "live",
    label: "Windows 앱",
    updated: "2026-08-19",
    now: "국가별 방화벽 차단. 구 ChinaGuard 규칙 자동 정리.",
    next: "Windows에서만 빌드(build.cmd).",
    docs: [{ title: "README", git: "GeoGuard/README.md" }, { title: "설치", git: "GeoGuard/설치가이드.txt" }],
  },
  docs: {
    id: "docs",
    phase: "docs",
    label: "문서",
    updated: "2026-09-09",
    now: "STATUS=재와별(08-20). SESSION_HANDOFF=Ulon(09-09).",
    next: "archive는 운영 지침으로 쓰지 않음.",
    docs: LAB_DOCS.slice(5),
  },
  loop: {
    id: "loop",
    phase: "loop",
    label: "보드",
    updated: "2026-09-10",
    now: "board.html=재와별. command-board/=지휘 화면. BOARD.md=AI 원본.",
    next: "재와별 HTML을 덮지 말 것.",
    docs: [
      { title: "BOARD.md", git: "loop/BOARD.md" },
      { title: "지휘 화면", git: "loop/command-board/README.md" },
    ],
  },
};

export function statusOf(id: string): ProjectStatus {
  return (
    PROJECT_STATUS[id] || {
      id,
      phase: "idle",
      label: "기록 적음",
      updated: "—",
      now: "이 폴더는 README가 짧다. 문서를 읽고 추측으로 상태를 바꾸지 말 것.",
      next: "결정 대기에만 예/아니오로 올림.",
      docs: [],
    }
  );
}

export const PHASE_KO: Record<ProjectStatus["phase"], string> = {
  live: "라이브",
  loop: "루프",
  review: "검수",
  idle: "대기",
  docs: "문서",
};
