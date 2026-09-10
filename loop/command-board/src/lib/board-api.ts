import type { BoardCard } from "./board-types";

export type BoardSnapshot = {
  ok: true;
  head: string;
  root: string;
  command?: { at: string; projectId: string; git: string; body: string };
  cards: BoardCard[];
  roster: string[];
  dispatch: { running: boolean; since?: string };
  tools: Record<string, boolean>;
};

export type WriteResult = { ok: true; sha?: string; pushed?: boolean; note?: string };

export async function fetchBoard(): Promise<BoardSnapshot> {
  const r = await fetch("/api/board", { cache: "no-store" });
  const j = (await r.json().catch(() => ({}))) as Partial<BoardSnapshot> & { error?: string };
  if (!r.ok || !j.ok) throw new Error(j.error || "보드 서버 응답 없음 (" + r.status + ")");
  return j as BoardSnapshot;
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
