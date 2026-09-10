import { PROJECT_STATUS, statusOf, type ProjectStatus } from "./lab-status";
import { findLab } from "./lab-tree";

/**
 * 프로젝트별 「지금 어디」 — 1순위 로컬 git(보드 서버 /api/status), 2순위 GitHub API.
 * 하드코딩 요약은 서버가 없을 때의 마지막 폴백일 뿐이다.
 */

const OWNER = "crossx362-commits";
const REPO = "ai_lab";

export type LivePatch = {
  updated: string;
  sha: string;
  now: string;
  next: string;
  source: string;
};

type Commit = { date: string; sha: string; message: string };

/** 파일 머리를 읽어 요약을 뽑는 프로젝트(그 외는 최신 커밋 메시지) */
const FILE_OF: Record<string, string> = {
  ashes: "docs/STATUS.md",
  ulon: "docs/SESSION_HANDOFF.md",
  loop: "loop/BOARD.md",
};

function day(iso?: string) {
  return iso ? iso.slice(0, 10) : "—";
}

function shortSha(sha?: string) {
  return sha ? sha.slice(0, 7) : "";
}

function fallbackOf(id: string, source: string): LivePatch {
  const st = statusOf(id);
  return { updated: st.updated, sha: "", now: st.now, next: st.next, source };
}

// ---------- 1순위: 로컬 git ----------

async function localStatus(rel: string, file?: string): Promise<{ commit: Commit; head: string } | null> {
  const q = new URLSearchParams({ path: rel });
  if (file) q.set("file", file);
  const res = await fetch("/api/status?" + q.toString(), { cache: "no-store" });
  if (!res.ok) return null;
  const j = (await res.json()) as { ok: boolean; sha: string; date: string; message: string; head: string };
  if (!j.ok) return null;
  return { commit: { date: j.date, sha: j.sha, message: j.message }, head: j.head || "" };
}

// ---------- 2순위: GitHub ----------

async function latestCommit(rel: string): Promise<Commit | null> {
  const url =
    `https://api.github.com/repos/${OWNER}/${REPO}/commits?sha=master&per_page=1` +
    (rel ? `&path=${encodeURIComponent(rel)}` : "");
  const res = await fetch(url);
  if (!res.ok) return null;
  const json = (await res.json()) as { sha?: string; commit?: { message?: string; committer?: { date?: string } } }[];
  const row = json[0];
  if (!row?.sha) return null;
  return {
    date: day(row.commit?.committer?.date),
    sha: shortSha(row.sha),
    message: (row.commit?.message || "").split("\n")[0] || "",
  };
}

async function fileHead(rel: string): Promise<string> {
  const res = await fetch(`https://raw.githubusercontent.com/${OWNER}/${REPO}/master/${rel}`);
  if (!res.ok) return "";
  const text = await res.text();
  return text.split("\n").slice(0, 24).join("\n");
}

// ---------- 요약 ----------

function parseStatus(head: string, commit: Commit): LivePatch {
  const updated = head.match(/최종 갱신:\s*([0-9-]+)/)?.[1] || commit.date;
  const next = head.match(/폴리싱 다음:\s*\**([^*\n]+)\**/)?.[1]?.trim() || "파티";
  const now =
    head
      .match(/최종 갱신:[^\n]+/)?.[0]
      ?.replace(/^최종 갱신:\s*/, "")
      .slice(0, 80) || commit.message;
  return { updated, sha: commit.sha, now, next: "폴리싱 다음 = " + next, source: "docs/STATUS.md" };
}

function parseHandoff(head: string, commit: Commit): LivePatch {
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

function parseBoard(head: string, commit: Commit): LivePatch {
  const cmd = head.match(/^## 명령\n- (.+)$/m)?.[1] || "";
  return {
    updated: commit.date,
    sha: commit.sha,
    now: cmd ? "현재 명령: " + cmd.replace(/^\[[^\]]+\] \([^)]*\) /, "").slice(0, 80) : commit.message.slice(0, 90),
    next: "의견 → 채택 → 실행",
    source: "loop/BOARD.md",
  };
}

function summarize(id: string, rel: string, commit: Commit, head: string, fallbackNext: string): LivePatch {
  const file = FILE_OF[id];
  if (file === "docs/STATUS.md" && head) return parseStatus(head, commit);
  if (file === "docs/SESSION_HANDOFF.md" && head) return parseHandoff(head, commit);
  if (file === "loop/BOARD.md" && head) return parseBoard(head, commit);
  return { updated: commit.date, sha: commit.sha, now: commit.message.slice(0, 90), next: fallbackNext, source: rel || "master" };
}

export async function fetchLive(id: string): Promise<LivePatch> {
  const rel = findLab(id)?.git || "";
  const file = FILE_OF[id];
  const fallback = fallbackOf(id, rel || id);
  try {
    const local = await localStatus(rel, file).catch(() => null);
    if (local) return { ...summarize(id, rel, local.commit, local.head, fallback.next), source: "로컬 git · " + (file || rel || "master") };
    const commit = await latestCommit(rel);
    if (!commit) return fallback;
    const head = file ? await fileHead(file) : "";
    return summarize(id, rel, commit, head, fallback.next);
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
