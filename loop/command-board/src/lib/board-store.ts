import { create } from "zustand";
import type { BoardCard, Verdict } from "./board-types";
import { fetchBoard, postJson, type WriteResult } from "./board-api";
import { findLab } from "./lab-tree";

const PROJECT_KEY = "command-board:project";
const POLL_MS = 5000;

type BoardState = {
  ready: boolean;
  online: boolean;
  busy: boolean;
  error: string;
  notice: string;
  head: string;
  dispatchRunning: boolean;
  tools: Record<string, boolean>;
  cards: BoardCard[];
  activeProjectId: string;
  hydrate: () => void;
  refresh: () => Promise<void>;
  setProject: (id: string) => void;
  addCommand: (text: string) => Promise<void>;
  gatherOpinions: () => Promise<void>;
  decide: (id: string, verdict: Verdict) => Promise<void>;
};

let timer: number | undefined;

function savedProject() {
  try {
    return window.localStorage.getItem(PROJECT_KEY) || "lab";
  } catch {
    return "lab";
  }
}

function writeNote(r: WriteResult) {
  if (r.note) return r.note;
  return r.pushed ? "저장·푸시됨 " + (r.sha || "") : "저장됨(푸시 안 됨) " + (r.sha || "");
}

export const useBoardStore = create<BoardState>((set, get) => ({
  ready: false,
  online: false,
  busy: false,
  error: "",
  notice: "",
  head: "",
  dispatchRunning: false,
  tools: {},
  cards: [],
  activeProjectId: "lab",

  hydrate: () => {
    if (typeof window === "undefined") return;
    set({ activeProjectId: savedProject() });
    void get().refresh();
    if (timer === undefined) {
      timer = window.setInterval(() => void get().refresh(), POLL_MS);
    }
  },

  refresh: async () => {
    try {
      const snap = await fetchBoard();
      set({
        ready: true,
        online: true,
        error: "",
        head: snap.head,
        cards: snap.cards,
        dispatchRunning: snap.dispatch.running,
        tools: snap.tools,
      });
    } catch (e) {
      set({ ready: true, online: false, error: e instanceof Error ? e.message : String(e) });
    }
  },

  setProject: (id) => {
    set({ activeProjectId: id });
    try {
      window.localStorage.setItem(PROJECT_KEY, id);
    } catch {
      /* 저장 못 해도 동작에는 지장 없음 */
    }
  },

  addCommand: async (text) => {
    const trimmed = text.trim();
    if (!trimmed || get().busy) return;
    const node = findLab(get().activeProjectId);
    set({ busy: true, error: "", notice: "" });
    try {
      const r = await postJson<WriteResult>("/api/command", {
        text: trimmed,
        projectId: get().activeProjectId,
        git: node?.git || "",
      });
      set({ notice: "명령 " + writeNote(r) + " · 의견 수집 시작" });
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
    } finally {
      set({ busy: false });
      await get().refresh();
    }
  },

  gatherOpinions: async () => {
    if (get().busy) return;
    set({ busy: true, error: "", notice: "" });
    try {
      const r = await postJson<{ ok: true; started: boolean; note: string }>("/api/dispatch", {});
      set({ notice: r.started ? "의견 수집 시작 — CLI 4종 병렬, 수 분 걸림" : r.note });
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
    } finally {
      set({ busy: false });
      await get().refresh();
    }
  },

  decide: async (id, verdict) => {
    if (get().busy) return;
    set({ busy: true, error: "", notice: "" });
    try {
      const r = await postJson<WriteResult>("/api/decide", { id, verdict });
      set({ notice: verdict + " " + writeNote(r) });
    } catch (e) {
      set({ error: e instanceof Error ? e.message : String(e) });
    } finally {
      set({ busy: false });
      await get().refresh();
    }
  },
}));
