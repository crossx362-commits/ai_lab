import { create } from "zustand";
import { SAMPLE_CARDS, type BoardCard, type ColumnId, type Verdict } from "./board-types";
import { findLab } from "./lab-tree";
import { draftOpinions } from "./ai-roster";
import { assignsWorkToOwner } from "./owner-rules";

const STORAGE_KEY = "command-board-v6";

function uid(prefix: string) {
  return prefix + Math.random().toString(36).slice(2, 9);
}

type BoardState = {
  ready: boolean;
  cards: BoardCard[];
  activeProjectId: string;
  hydrate: () => void;
  persist: () => void;
  setProject: (id: string) => void;
  addCommand: (text: string) => void;
  gatherOpinions: () => void;
  decide: (id: string, verdict: Verdict) => void;
  move: (id: string, col: ColumnId) => void;
};

type Saved = { cards?: BoardCard[]; activeProjectId?: string };

function load(): { cards: BoardCard[]; activeProjectId: string } {
  const fallback = { cards: SAMPLE_CARDS, activeProjectId: "petnna" };
  if (typeof window === "undefined") return fallback;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return fallback;
    const parsed = JSON.parse(raw) as Saved;
    return {
      cards: Array.isArray(parsed.cards) && parsed.cards.length ? parsed.cards : SAMPLE_CARDS,
      activeProjectId: parsed.activeProjectId || "petnna",
    };
  } catch {
    return fallback;
  }
}

export const useBoardStore = create<BoardState>((set, get) => ({
  ready: false,
  cards: SAMPLE_CARDS,
  activeProjectId: "petnna",
  hydrate: () => set({ ...load(), ready: true }),
  persist: () => {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ cards: get().cards, activeProjectId: get().activeProjectId }),
    );
  },
  setProject: (id) => {
    set({ activeProjectId: id });
    get().persist();
  },
  addCommand: (text) => {
    const trimmed = text.trim();
    if (!trimmed) return;
    const node = findLab(get().activeProjectId);
    const firstLine = trimmed.split("\n")[0]?.slice(0, 48) || "명령";
    const loc = node ? node.win + (node.git ? "\ngit:" + node.git : "") : "";
    const card: BoardCard = {
      id: uid("cmd"),
      col: "명령",
      who: "나",
      projectId: get().activeProjectId,
      title: firstLine,
      body: (loc ? loc + "\n" : "") + trimmed,
    };
    set({ cards: [card, ...get().cards] });
    get().persist();
    get().gatherOpinions();
  },
  gatherOpinions: () => {
    const cmd = get().cards.find((c) => c.col === "명령");
    if (!cmd) return;
    const projectId = cmd.projectId || get().activeProjectId;
    const existing = new Set(
      get()
        .cards.filter((c) => c.col === "의견" && c.from === cmd.id)
        .map((c) => c.who),
    );
    const fresh = draftOpinions(projectId, cmd.title)
      .filter((d) => !existing.has(d.who))
      .map((d) => ({ ...d, id: uid("op"), from: cmd.id }));
    if (!fresh.length) return;
    set({ cards: [...fresh, ...get().cards] });
    get().persist();
  },
  decide: (id, verdict) => {
    const cards = get().cards;
    const source = cards.find((c) => c.id === id);
    if (!source || source.verdict) return;
    const next: BoardCard[] = cards.map((c) => (c.id === id ? { ...c, verdict } : c));
    if (verdict === "채택") {
      next.unshift({
        id: uid("dec"),
        col: "결정",
        who: "나",
        projectId: source.projectId || get().activeProjectId,
        title: "채택: " + source.title,
        body: (source.who ? source.who + " 의견\n" : "") + (source.body || ""),
        from: id,
      });
      next.unshift({
        id: uid("run"),
        col: "실행",
        who: source.who === "Claude" ? "Claude" : "Grok Build",
        projectId: source.projectId || get().activeProjectId,
        title: "실행: " + source.title,
        body: "채택됨. 해당 git 경로만.",
        from: id,
      });
    }
    set({ cards: next });
    get().persist();
  },
  move: (id, col) => {
    const card = get().cards.find((c) => c.id === id);
    const dest =
      col === "질문" && card && assignsWorkToOwner(card.title + "\n" + card.body) ? "결정대기" : col;
    set({
      cards: get().cards.map((c) => (c.id === id ? { ...c, col: dest } : c)),
    });
    get().persist();
  },
}));
