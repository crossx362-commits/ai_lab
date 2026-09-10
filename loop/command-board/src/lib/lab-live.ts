import { PROJECT_STATUS, statusOf, type ProjectStatus } from "./lab-status";

const OWNER = "crossx362-commits";
const REPO = "ai_lab";

export type LivePatch = {
  updated: string;
  sha: string;
  now: string;
  next: string;
  source: string;
};

const PATHS: Record<string, { path: string; file?: string }> = {
  ashes: { path: "docs/STATUS.md", file: "docs/STATUS.md" },
  ulon: { path: "docs/SESSION_HANDOFF.md", file: "docs/SESSION_HANDOFF.md" },
  petnna: { path: "projects/petnna", file: "projects/petnna/CHANGELOG.md" },
  "ai-team": { path: "projects/ai-team", file: "projects/ai-team/README.md" },
  homepage: { path: "projects/homepage" },
  geoguard: { path: "GeoGuard", file: "GeoGuard/README.md" },
  chinaguard: { path: "ChinaGuard" },
  loop: { path: "loop", file: "loop/BOARD.md" },
  lab: { path: "" },
  docs: { path: "docs" },
};

export const LIVE_FALLBACK: Record<string, LivePatch> = {
  lab: {
    updated: "2026-09-10",
    sha: "e29379c",
    now: "master HEAD. 지휘 보드 loop/BOARD.md 추가.",
    next: "시크릿·배포는 멈춤.",
    source: "master",
  },
  petnna: {
    updated: "2026-08-16",
    sha: "f91107c",
    now: "마지막 커밋: 리소스 아틀라스 연동. CHANGELOG는 v1.3.0 (2026-06-11).",
    next: "봄이 QA → 수리.",
    source: "projects/petnna",
  },
  "ai-team": {
    updated: "2026-09-03",
    sha: "822ffd5",
    now: "텔레그램 연동·자동 스케줄 영구 비활성화.",
    next: "데몬은 맥, harness만.",
    source: "projects/ai-team",
  },
  ashes: {
    updated: "2026-08-20",
    sha: "53fb7e4",
    now: "캐릭터 속성 패널 밀도 노출. 마지막 트랙=코드.",
    next: "폴리싱 다음 = 파티",
    source: "docs/STATUS.md",
  },
  ulon: {
    updated: "2026-09-09",
    sha: "958c3c0",
    now: "수면 되풀이 도장 랩. Grok 차선 B 닫힘. HUD 재작성.",
    next: "다음 큐 A: Selected/Active* → WorldBody",
    source: "docs/SESSION_HANDOFF.md",
  },
  homepage: {
    updated: "2026-08-20",
    sha: "195faf4",
    now: "코드 검토 10건 반영. 블로그·방명록 라이브.",
    next: "콘텐츠만. 엔진 루프와 분리.",
    source: "projects/homepage",
  },
  geoguard: {
    updated: "2026-08-19",
    sha: "ab14925",
    now: "국가별 방화벽. 구 ChinaGuard 규칙 정리.",
    next: "Windows build.cmd만.",
    source: "GeoGuard",
  },
  chinaguard: {
    updated: "2026-08-19",
    sha: "ab14925",
    now: "구버전. GeoGuard가 후속.",
    next: "신규는 GeoGuard.",
    source: "ChinaGuard",
  },
  loop: {
    updated: "2026-09-10",
    sha: "e29379c",
    now: "공유 BOARD.md. 재와별 board.html 유지.",
    next: "재와별 HTML을 덮지 말 것.",
    source: "loop/BOARD.md",
  },
  docs: {
    updated: "2026-09-09",
    sha: "958c3c0",
    now: "STATUS=재와별. SESSION_HANDOFF=Ulon.",
    next: "archive는 운영 지침 아님.",
    source: "docs/",
  },
};

function day(iso?: string) {
  return iso ? iso.slice(0, 10) : "—";
}

function shortSha(sha?: string) {
  return sha ? sha.slice(0, 7) : "";
}

async function latestCommit(path: string): Promise<{ date: string; sha: string; message: string } | null> {
  const url =
    `https://api.github.com/repos/${OWNER}/${REPO}/commits?sha=master&per_page=1` +
    (path ? `&path=${encodeURIComponent(path)}` : "");
  const res = await fetch(url);
  if (!res.ok) return null;
  const json = (await res.json()) as {
    sha?: string;
    commit?: { message?: string; committer?: { date?: string } };
  }[];
  const row = json[0];
  if (!row?.sha) return null;
  return {
    date: day(row.commit?.committer?.date),
    sha: shortSha(row.sha),
    message: (row.commit?.message || "").split("\n")[0] || "",
  };
}

async function fileHead(path: string): Promise<string> {
  const res = await fetch(`https://raw.githubusercontent.com/${OWNER}/${REPO}/master/${path}`);
  if (!res.ok) return "";
  const text = await res.text();
  return text.split("\n").slice(0, 24).join("\n");
}

function parseStatus(head: string, commit: { date: string; sha: string; message: string }): LivePatch {
  const updated = head.match(/최종 갱신:\s*([0-9-]+)/)?.[1] || commit.date;
  const next = head.match(/폴리싱 다음:\s*\**([^*\n]+)\**/)?.[1]?.trim() || "파티";
  const now =
    head
      .match(/최종 갱신:[^\n]+/)?.[0]
      ?.replace(/^최종 갱신:\s*/, "")
      .slice(0, 80) || commit.message;
  return { updated, sha: commit.sha, now, next: "폴리싱 다음 = " + next, source: "docs/STATUS.md" };
}

function parseHandoff(head: string, commit: { date: string; sha: string; message: string }): LivePatch {
  const grok = /Grok 차선 B 닫힘/.test(head);
  const next = head.match(/다음 큐 A[^\n]*/)?.[0] || "화면 게이트";
  return {
    updated: commit.date,
    sha: commit.sha,
    now: (grok ? "Grok 차선 B 닫힘. " : "") + commit.message.slice(0, 72),
    next,
    source: "docs/SESSION_HANDOFF.md",
  };
}

export async function fetchLive(id: string): Promise<LivePatch> {
  const spec = PATHS[id] || { path: "" };
  const fallback = LIVE_FALLBACK[id] || {
    updated: statusOf(id).updated,
    sha: "",
    now: statusOf(id).now,
    next: statusOf(id).next,
    source: spec.path || id,
  };
  try {
    const commit = await latestCommit(spec.path);
    if (!commit) return fallback;
    if (spec.file === "docs/STATUS.md") {
      const head = await fileHead(spec.file);
      if (head) return parseStatus(head, commit);
    }
    if (spec.file === "docs/SESSION_HANDOFF.md") {
      const head = await fileHead(spec.file);
      if (head) return parseHandoff(head, commit);
    }
    return {
      updated: commit.date,
      sha: commit.sha,
      now: commit.message.slice(0, 90),
      next: fallback.next,
      source: spec.path || "master",
    };
  } catch {
    return fallback;
  }
}

export function mergeStatus(id: string, live?: LivePatch): ProjectStatus {
  const base = PROJECT_STATUS[id] || statusOf(id);
  if (!live) return base;
  return {
    ...base,
    updated: live.updated + (live.sha ? " · " + live.sha : ""),
    now: live.now,
    next: live.next,
  };
}
