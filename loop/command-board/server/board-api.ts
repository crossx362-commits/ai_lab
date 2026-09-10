/**
 * 지휘 보드 로컬 API — Vite 플러그인.
 *
 * 원본은 `loop/BOARD.md` 하나다. 화면은 이 파일을 읽어 카드로 보여주고,
 * 명령·채택·반려는 이 파일에 쓴 뒤 같은 호흡으로 커밋·푸시한다.
 * 의견은 `loop/dispatch-board.sh`가 구독 CLI(claude/codex/gemini/grok)를 불러
 * `loop/opinions/<AI>.md`에 남긴 것을 그대로 읽는다 — 화면이 의견을 지어내지 않는다.
 */
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createHash } from "node:crypto";
import { execFile, spawn, type ChildProcess } from "node:child_process";
import { fileURLToPath } from "node:url";
import type { IncomingMessage, ServerResponse } from "node:http";
import type { Plugin } from "vite";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "..", "..", "..");
const BOARD = path.join(ROOT, "loop", "BOARD.md");
const OPINIONS = path.join(ROOT, "loop", "opinions");
const DISPATCH = path.join(ROOT, "loop", "dispatch-board.sh");
const BRANCH = "master";
/** 의견 파일을 내는 CLI 4종의 표시 순서. "Grok Build"는 실행 담당이라 의견 칸엔 없다. */
const ROSTER = ["Grok", "GPT", "제미니", "Claude"];

const SEC = {
  cmd: "명령",
  op: "의견·초안",
  pending: "결정대기",
  decided: "결정",
  run: "실행",
  q: "질문",
} as const;

type Card = {
  id: string;
  col: "명령" | "의견" | "결정대기" | "결정" | "실행" | "질문";
  who: string;
  title: string;
  body: string;
  projectId?: string;
  verdict?: "채택" | "반려";
  status?: "running" | "ok" | "fail" | "none";
  done?: boolean;
  at?: string;
};

// ---------- BOARD.md 읽기/쓰기 ----------

type Section = { name: string; lines: string[] };

function readBoard() {
  return fs.readFileSync(BOARD, "utf8");
}

function split(md: string): { head: string[]; sections: Section[] } {
  const head: string[] = [];
  const sections: Section[] = [];
  let cur: Section | null = null;
  for (const line of md.split(/\r?\n/)) {
    const m = /^## (.+?)\s*$/.exec(line);
    if (m) {
      cur = { name: m[1], lines: [] };
      sections.push(cur);
      continue;
    }
    (cur ? cur.lines : head).push(line);
  }
  return { head, sections };
}

function join(head: string[], sections: Section[]) {
  const out = [...head];
  while (out.length && out[out.length - 1] === "") out.pop();
  for (const s of sections) {
    const lines = [...s.lines];
    while (lines.length && lines[lines.length - 1] === "") lines.pop();
    out.push("", "## " + s.name, ...lines);
  }
  return out.join("\n").replace(/\n+$/, "") + "\n";
}

/** 항목 = `- 텍스트` 한 줄. `-` 하나만 있는 줄은 빈 자리 표시. */
function items(sec: Section | undefined): string[] {
  if (!sec) return [];
  const out: string[] = [];
  for (const line of sec.lines) {
    const m = /^- ?(.*)$/.exec(line);
    if (m && m[1].trim()) out.push(m[1].trim());
  }
  return out;
}

function setItems(sec: Section, list: string[]) {
  sec.lines = list.length ? list.map((t) => "- " + t) : ["-"];
}

function section(sections: Section[], name: string, createBefore?: string): Section {
  let s = sections.find((x) => x.name === name);
  if (s) return s;
  s = { name, lines: ["-"] };
  const idx = createBefore ? sections.findIndex((x) => x.name === createBefore) : -1;
  if (idx >= 0) sections.splice(idx, 0, s);
  else sections.push(s);
  return s;
}

function stamp(d = new Date()) {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

function idOf(section: string, text: string) {
  return createHash("sha1").update(section + "|" + text).digest("hex").slice(0, 12);
}

function oneLine(s: string) {
  return s.replace(/\r?\n+/g, " / ").replace(/\s+/g, " ").trim();
}

// ---------- 카드 만들기 ----------

const CMD_RE = /^\[([^\]]+)\] \(([^)]*)\) (.*)$/;
const VERDICT_RE = /^\[([^\]]+)\] (채택|반려): (.+?) — (.*)$/;
const ANSWER_RE = /^(.*?) → (예|아니오) \[([^\]]*)\]$/;

function parseCommand(text: string) {
  const m = CMD_RE.exec(text);
  if (!m) return { at: "", projectId: "", git: "", body: text };
  const [projectId, git = ""] = m[2].split(" · ").map((x) => x.trim());
  return { at: m[1], projectId, git, body: m[3] };
}

function opinionFiles() {
  if (!fs.existsSync(OPINIONS))
    return [] as { who: string; text: string; status: Card["status"]; err: string; mtime: number }[];
  return fs
    .readdirSync(OPINIONS)
    .filter((f) => f.endsWith(".md") && !f.startsWith("_"))
    .map((f) => {
      const who = f.slice(0, -3);
      const p = path.join(OPINIONS, f);
      const text = fs.readFileSync(p, "utf8").trim();
      const sp = path.join(OPINIONS, who + ".status");
      const raw = fs.existsSync(sp) ? fs.readFileSync(sp, "utf8").trim().split(/\s+/)[0] : "";
      const status: Card["status"] =
        raw === "running" || raw === "ok" || raw === "fail" ? raw : text === who + " 실패" ? "fail" : "none";
      const ep = path.join(OPINIONS, who + ".err");
      const err = status === "fail" && fs.existsSync(ep) ? failureReason(fs.readFileSync(ep, "utf8")) : "";
      return { who, text, status, err, mtime: fs.statSync(p).mtimeMs };
    });
}

/** err 파일에서 사람이 읽을 한 줄 — 인증·한도·미설치가 대부분이다. */
function failureReason(err: string) {
  const lines = err
    .replace(/\x1b\[[0-9;]*m/g, "")
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean);
  // 우선순위: 인증·한도(사람이 풀어야 함) → 미설치 → 그 밖의 Error → 마지막 줄
  const tiers = [
    /not signed in|usage limit|quota|authenticate|expired|log ?in|rate limit/i,
    /없음|not found|command not found/i,
    /error/i,
  ];
  for (const re of tiers) {
    const hit = lines.find((l) => re.test(l));
    if (hit) return hit.slice(0, 220);
  }
  return (lines[lines.length - 1] || "").slice(0, 220);
}

function buildCards(md: string): { cards: Card[]; command?: ReturnType<typeof parseCommand> } {
  const { sections } = split(md);
  const get = (n: string) => sections.find((s) => s.name === n);
  const cards: Card[] = [];

  const cmds = items(get(SEC.cmd));
  const current = cmds.length ? parseCommand(cmds[0]) : undefined;
  for (const t of cmds) {
    const c = parseCommand(t);
    cards.push({
      id: idOf(SEC.cmd, t),
      col: "명령",
      who: "나",
      title: c.body.slice(0, 48),
      body: (c.git ? "git: " + c.git + "\n" : "") + c.body,
      projectId: c.projectId || undefined,
      at: c.at,
    });
  }

  const verdicts = new Map<string, "채택" | "반려">();
  for (const t of items(get(SEC.op))) {
    const m = VERDICT_RE.exec(t);
    if (m) verdicts.set(m[3].trim() + "|" + m[4].trim(), m[2] as "채택" | "반려");
  }

  const files = opinionFiles();
  const order = (w: string) => {
    const i = ROSTER.indexOf(w);
    return i < 0 ? 99 : i;
  };
  files.sort((a, b) => order(a.who) - order(b.who) || a.who.localeCompare(b.who));
  for (const f of files) {
    const lines = f.text.split(/\r?\n/).filter((l) => l.trim());
    const failed = f.status === "fail";
    const title = failed ? "응답 없음" : (lines[0] || "(빈 응답)").slice(0, 80);
    cards.push({
      id: idOf("의견", f.who + "|" + title),
      col: "의견",
      who: f.who,
      title,
      body: failed ? (f.err || "CLI 실패") + "\n(loop/opinions/" + f.who + ".err)" : lines.slice(1).join("\n"),
      projectId: current?.projectId || undefined,
      verdict: verdicts.get(f.who + "|" + title),
      status: f.status,
    });
  }

  for (const t of items(get(SEC.pending))) {
    const m = ANSWER_RE.exec(t);
    cards.push({
      id: idOf(SEC.pending, t),
      col: "결정대기",
      who: "오너",
      title: m ? m[1] : t,
      body: m ? "답: " + m[2] + " (" + m[3] + ")" : "예 / 아니오",
      verdict: m ? (m[2] === "예" ? "채택" : "반려") : undefined,
    });
  }
  for (const t of items(get(SEC.decided))) {
    cards.push({ id: idOf(SEC.decided, t), col: "결정", who: "나", title: t, body: "" });
  }
  for (const t of items(get(SEC.run))) {
    const m = /^\[([ xX])\] (.*)$/.exec(t);
    cards.push({
      id: idOf(SEC.run, t),
      col: "실행",
      who: "보드",
      title: m ? m[2] : t,
      body: "",
      done: m ? m[1] !== " " : false,
    });
  }
  for (const t of items(get(SEC.q))) {
    const m = ANSWER_RE.exec(t);
    cards.push({
      id: idOf(SEC.q, t),
      col: "질문",
      who: "AI",
      title: m ? m[1] : t,
      body: m ? "답: " + m[2] + " (" + m[3] + ")" : "예 / 아니오",
      verdict: m ? (m[2] === "예" ? "채택" : "반려") : undefined,
    });
  }
  return { cards, command: current };
}

// ---------- git ----------

function run(cmd: string, args: string[], opts: { cwd?: string } = {}) {
  return new Promise<{ ok: boolean; out: string; err: string }>((resolve) => {
    execFile(
      cmd,
      args,
      { cwd: opts.cwd || ROOT, windowsHide: true, maxBuffer: 4 << 20, env: { ...process.env, LANG: "C.UTF-8" } },
      (e, out, err) => resolve({ ok: !e, out: String(out || ""), err: String(err || "") }),
    );
  });
}

const git = (args: string[]) => run("git", args);

async function commitBoard(message: string) {
  const msgFile = path.join(os.tmpdir(), "command-board-msg.txt");
  fs.writeFileSync(msgFile, message + "\n", "utf8");
  // 판정과 근거가 같이 이동해야 한다(Claude 의견 2026-09-10): 현재 의견 원문 4개(*.md)도 함께 커밋.
  // archive/·상태·err 파일은 .gitignore로 제외돼 저장소가 불지 않는다.
  await git(["add", "-A", "--", "loop/BOARD.md", "loop/opinions"]);
  const c = await git(["commit", "-F", msgFile, "--", "loop/BOARD.md", "loop/opinions"]);
  if (!c.ok && !/nothing to commit|nothing added/.test(c.out + c.err)) {
    return { sha: "", pushed: false, note: "커밋 실패: " + (c.err || c.out).trim().slice(-300) };
  }
  const sha = (await git(["rev-parse", "--short", "HEAD"])).out.trim();
  let p = await git(["push", "origin", BRANCH]);
  if (!p.ok) {
    const r = await git(["pull", "--rebase", "--autostash", "origin", BRANCH]);
    if (r.ok) p = await git(["push", "origin", BRANCH]);
    else await git(["rebase", "--abort"]);
  }
  return { sha, pushed: p.ok, note: p.ok ? "" : "푸시 실패(로컬 커밋은 됨): " + (p.err || p.out).trim().slice(-300) };
}

// ---------- 디스패치(의견 수집) ----------

let child: ChildProcess | null = null;
let childSince = "";

function bashExe() {
  const cands = [
    process.env.BOARD_BASH,
    "C:\\Program Files\\Git\\bin\\bash.exe",
    "C:\\Program Files\\Git\\usr\\bin\\bash.exe",
    "/opt/homebrew/bin/bash",
    "/bin/bash",
  ].filter(Boolean) as string[];
  for (const c of cands) if (fs.existsSync(c)) return c;
  return "bash";
}

function dispatchState() {
  const sp = path.join(OPINIONS, "_dispatch.status");
  let file = "";
  if (fs.existsSync(sp)) file = fs.readFileSync(sp, "utf8").trim();
  const [state, ...rest] = file.split(/\s+/);
  // 밖에서 돌린 dispatch가 죽어 상태 파일만 running으로 남는 경우(제미니 지적) — CLI 타임아웃(7분)+여유를 넘기면 고착으로 본다
  const STALE_MS = 12 * 60_000;
  const fresh = state === "running" && Date.now() - fs.statSync(sp).mtimeMs < STALE_MS;
  return { running: Boolean(child) || fresh, since: child ? childSince : rest.join(" ") };
}

function startDispatch() {
  // 메모리의 child만 보면 서버 재시작 뒤 밖에서 도는 수집을 못 본다 — 상태 파일까지 같이 본다
  if (dispatchState().running) return { started: false, note: "이미 수집 중" };
  if (!fs.existsSync(DISPATCH)) return { started: false, note: "loop/dispatch-board.sh 없음" };
  fs.mkdirSync(OPINIONS, { recursive: true });
  childSince = stamp();
  // DISPATCH_LOG: 스크립트가 스스로 로그 파일에 쓴다. detached+unref로 dev 서버가 재시작·종료돼도
  // 수집이 끊기지 않는다(2026-09-10: 서버 코드 수정 → Vite 재시작 → 진행 중이던 수집이 죽은 사고).
  const env: NodeJS.ProcessEnv = {
    ...process.env,
    DISPATCH_ONCE: "1",
    DISPATCH_FORCE: "1",
    DISPATCH_LOG: path.join(OPINIONS, "_dispatch.log"),
    PYTHONUTF8: "1",
  };
  delete env.CLAUDECODE; // 클로드 세션에서 띄운 dev 서버라도 자식 claude -p가 중첩 거부되지 않게
  delete env.CLAUDE_CODE_ENTRYPOINT;
  const proc = spawn(bashExe(), [DISPATCH], {
    cwd: ROOT,
    env,
    stdio: "ignore",
    detached: true,
    windowsHide: true,
  });
  proc.unref();
  child = proc;
  const done = () => {
    child = null;
  };
  proc.on("exit", done);
  proc.on("error", done);
  return { started: true, note: "" };
}

/** 새 명령이 오면 이전 의견은 지우지 않고 archive/<시각>/ 로 옮긴다 — 판정 기록(BOARD.md)의 근거가 남아야 한다. */
function archiveOpinions() {
  if (!fs.existsSync(OPINIONS)) return;
  const files = fs.readdirSync(OPINIONS).filter((f) => !f.startsWith("_") && /\.(md|err|status|tmp)$/.test(f));
  if (!files.length) return;
  const dir = path.join(OPINIONS, "archive", stamp().replace(/[: ]/g, "-"));
  fs.mkdirSync(dir, { recursive: true });
  for (const f of files) {
    const src = path.join(OPINIONS, f);
    if (f.endsWith(".tmp")) fs.rmSync(src, { force: true });
    else fs.renameSync(src, path.join(dir, f));
  }
}

// ---------- 도구 상태 ----------

let toolCache: { at: number; value: Record<string, boolean> } | null = null;
async function toolStatus() {
  if (toolCache && Date.now() - toolCache.at < 60_000) return toolCache.value;
  const which = process.platform === "win32" ? "where" : "which";
  const value: Record<string, boolean> = {};
  for (const bin of ["claude", "codex", "gemini", "grok"]) value[bin] = (await run(which, [bin])).ok;
  value.grokLogin = fs.existsSync(path.join(os.homedir(), ".grok", "auth.json"));
  toolCache = { at: Date.now(), value };
  return value;
}

// ---------- HTTP ----------

function send(res: ServerResponse, code: number, body: unknown) {
  res.statusCode = code;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.setHeader("Cache-Control", "no-store");
  res.end(JSON.stringify(body));
}

function readJson(req: IncomingMessage) {
  return new Promise<Record<string, unknown>>((resolve, reject) => {
    let buf = "";
    req.setEncoding("utf8");
    req.on("data", (c) => {
      buf += c;
      if (buf.length > 1 << 20) reject(new Error("본문이 너무 큼"));
    });
    req.on("end", () => {
      try {
        resolve(buf ? (JSON.parse(buf) as Record<string, unknown>) : {});
      } catch (e) {
        reject(e);
      }
    });
    req.on("error", reject);
  });
}

async function handle(req: IncomingMessage, res: ServerResponse) {
  const url = new URL(req.url || "/", "http://x");
  const route = url.pathname.replace(/\/+$/, "");
  try {
    if (route === "/api/board" && req.method === "GET") {
      const md = readBoard();
      const { cards, command } = buildCards(md);
      const head = (await git(["rev-parse", "--short", "HEAD"])).out.trim();
      return send(res, 200, {
        ok: true,
        head,
        root: ROOT,
        command,
        cards,
        roster: ROSTER,
        dispatch: dispatchState(),
        tools: await toolStatus(),
      });
    }

    if (route === "/api/command" && req.method === "POST") {
      const body = await readJson(req);
      const text = oneLine(String(body.text || ""));
      if (!text) return send(res, 400, { ok: false, error: "명령이 비어 있음" });
      if (text.length > 2000) return send(res, 400, { ok: false, error: "명령은 2000자 이내" });
      const projectId = oneLine(String(body.projectId || "lab"));
      const gitPath = oneLine(String(body.git || ""));
      // 수집 중 새 명령을 받으면 진행 중 의견이 보관함으로 잘리고 새 수집은 건너뛰게 된다(Grok 의견 2026-09-10) — 끝날 때까지 거절
      if (dispatchState().running) {
        return send(res, 409, { ok: false, error: "의견 수집 중 — 끝난 뒤(수 분) 다시 내리세요" });
      }
      const md = readBoard();
      const { head, sections } = split(md);
      const cmd = section(sections, SEC.cmd, SEC.op);
      const line = `[${stamp()}] (${projectId}${gitPath ? " · " + gitPath : ""}) ${text}`;
      setItems(cmd, [line, ...items(cmd)]);
      fs.writeFileSync(BOARD, join(head, sections), "utf8");
      archiveOpinions();
      const c = await commitBoard(`board: 명령 — ${text.slice(0, 60)}`);
      const d = startDispatch();
      return send(res, 200, { ok: true, ...c, dispatch: d });
    }

    if (route === "/api/decide" && req.method === "POST") {
      const body = await readJson(req);
      const id = String(body.id || "");
      const verdict = body.verdict === "채택" ? "채택" : body.verdict === "반려" ? "반려" : null;
      if (!id || !verdict) return send(res, 400, { ok: false, error: "id·verdict 필요" });
      const md = readBoard();
      const { cards } = buildCards(md);
      const card = cards.find((c) => c.id === id);
      if (!card) return send(res, 404, { ok: false, error: "카드를 찾을 수 없음(보드가 바뀜) — 새로고침" });
      if (card.verdict) return send(res, 409, { ok: false, error: "이미 판정됨" });
      const { head, sections } = split(md);
      const ts = stamp();
      let summary = "";
      if (card.col === "의견") {
        const op = section(sections, SEC.op, SEC.pending);
        setItems(op, [...items(op), `[${ts}] ${verdict}: ${card.who} — ${card.title}`]);
        if (verdict === "채택") {
          const dec = section(sections, SEC.decided, SEC.run);
          setItems(dec, [...items(dec), `[${ts}] 채택: ${card.title} (${card.who})`]);
          const runSec = section(sections, SEC.run, SEC.q);
          const executor = card.who === "Claude" ? "Claude" : "Grok Build";
          setItems(runSec, [...items(runSec), `[ ] [${ts}] ${executor}: ${card.title}`]);
        }
        summary = `${verdict} — ${card.who}: ${card.title}`;
      } else if (card.col === "결정대기" || card.col === "질문") {
        // 질문 칸도 예/아니오만 받는다(BOARD.md 금지 절). 답은 줄 끝에 남기고, 예면 「결정」에도 적는다.
        const secName = card.col === "질문" ? SEC.q : SEC.pending;
        const target = section(sections, secName, card.col === "질문" ? undefined : SEC.decided);
        const answer = verdict === "채택" ? "예" : "아니오";
        setItems(
          target,
          items(target).map((t) => (idOf(secName, t) === id ? `${t} → ${answer} [${ts}]` : t)),
        );
        const dec = section(sections, SEC.decided, SEC.run);
        setItems(dec, [...items(dec), `[${ts}] ${answer}: ${card.title}`]);
        summary = `${answer} — ${card.title}`;
      } else {
        return send(res, 400, { ok: false, error: "이 칸은 판정 대상이 아님" });
      }
      fs.writeFileSync(BOARD, join(head, sections), "utf8");
      const c = await commitBoard(`board: 결정 — ${summary.slice(0, 60)}`);
      return send(res, 200, { ok: true, ...c });
    }

    if (route === "/api/dispatch" && req.method === "POST") {
      const md = readBoard();
      const { command } = buildCards(md);
      if (!command) return send(res, 400, { ok: false, error: "명령이 없음 — 먼저 명령을 내리세요" });
      return send(res, 200, { ok: true, ...startDispatch() });
    }

    if (route === "/api/status" && req.method === "GET") {
      // 로컬 git으로 경로별 최신 커밋 — GitHub API 한도(시간당 60회)·오프라인에 안 걸린다
      const rel = (url.searchParams.get("path") || "").replace(/\\/g, "/").replace(/^\/+|\/+$/g, "");
      const abs = path.resolve(ROOT, rel);
      if (!abs.startsWith(ROOT)) return send(res, 400, { ok: false, error: "저장소 밖 경로" });
      const args = ["log", "-1", "--format=%h%x1f%cI%x1f%s"];
      if (rel) args.push("--", rel);
      const r = await git(args);
      const [sha = "", date = "", message = ""] = r.out.trim().split("\x1f");
      let headText = "";
      const file = (url.searchParams.get("file") || "").replace(/\\/g, "/");
      if (file && /\.md$/i.test(file)) {
        const fabs = path.resolve(ROOT, file);
        if (fabs.startsWith(ROOT) && fs.existsSync(fabs)) {
          headText = fs.readFileSync(fabs, "utf8").split(/\r?\n/).slice(0, 24).join("\n");
        }
      }
      return send(res, 200, { ok: r.ok && Boolean(sha), sha, date: date.slice(0, 10), message, head: headText, path: rel });
    }

    if (route === "/api/dispatch/log" && req.method === "GET") {
      const p = path.join(OPINIONS, "_dispatch.log");
      const text = fs.existsSync(p) ? fs.readFileSync(p, "utf8").split("\n").slice(-60).join("\n") : "";
      return send(res, 200, { ok: true, text });
    }

    return send(res, 404, { ok: false, error: "없는 경로" });
  } catch (e) {
    return send(res, 500, { ok: false, error: e instanceof Error ? e.message : String(e) });
  }
}

export function boardApi(): Plugin {
  const install = (server: { middlewares: { use: (p: string, h: (req: IncomingMessage, res: ServerResponse) => void) => void } }) => {
    server.middlewares.use("/api", (req, res) => {
      req.url = "/api" + (req.url || "");
      void handle(req, res);
    });
  };
  return {
    name: "command-board-api",
    configureServer: install,
    configurePreviewServer: install,
  };
}
