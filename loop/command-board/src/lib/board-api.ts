import type { BoardCard } from "./board-types";

export const PROJECT_STATES = ["진행", "보류", "완료", "접음"] as const;
export type ProjectState = (typeof PROJECT_STATES)[number];
export type ProjectStateRow = { state: ProjectState; at: string; note: string };

export type BoardSnapshot = {
  ok: true;
  head: string;
  root: string;
  command?: { at: string; projectId: string; git: string; body: string };
  cards: BoardCard[];
  roster: string[];
  projects: Record<string, ProjectStateRow>;
  dispatch: { running: boolean; since?: string };
  tools: Record<string, boolean>;
};

export type ProjectGitRow = { id: string; path: string; sha: string; date: string; message: string; week: number };

export type WriteResult = { ok: true; sha?: string; pushed?: boolean; note?: string };

export async function fetchBoard(): Promise<BoardSnapshot> {
  const r = await fetch("/api/board", { cache: "no-store" });
  const j = (await r.json().catch(() => ({}))) as Partial<BoardSnapshot> & { error?: string };
  if (!r.ok || !j.ok) throw new Error(j.error || "보드 서버 응답 없음 (" + r.status + ")");
  return j as BoardSnapshot;
}

export async function fetchProjectGit(pairs: { id: string; git: string }[]): Promise<ProjectGitRow[]> {
  const q = pairs.map((p) => p.id + ":" + p.git).join(",");
  const r = await fetch("/api/projects?paths=" + encodeURIComponent(q), { cache: "no-store" });
  const j = (await r.json().catch(() => ({}))) as { ok?: boolean; rows?: ProjectGitRow[]; error?: string };
  if (!r.ok || !j.ok) throw new Error(j.error || "프로젝트 현황 응답 없음");
  return j.rows || [];
}

export async function postJson<T extends { ok: boolean; error?: string }>(url: string, body: unknown): Promise<T> {
  const r = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const j = (await r.json().catch(() => ({}))) as T;
  if (!r.ok || !j.ok) throw new Error(j.error || "요청 실패 (" + r.status + ")");
  return j;
}
