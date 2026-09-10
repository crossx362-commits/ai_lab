import { useEffect, useState } from "react";
import { useBoardStore } from "@/lib/board-store";
import { cn } from "@/lib/cn";
import { SAY } from "@/lib/words";

const CLI = ["claude", "codex", "gemini", "grok"] as const;

function ago(ms: number) {
  if (!ms) return "";
  const s = Math.max(0, Math.round((Date.now() - ms) / 1000));
  if (s < 60) return s + "초 전";
  const m = Math.round(s / 60);
  return m < 60 ? m + "분 전" : Math.round(m / 60) + "시간 전";
}

/** 연결·신선도·CLI 상태 한 줄. 오류는 배너로 올려 조용히 실패하지 않게 한다. */
export function StatusLine() {
  const online = useBoardStore((s) => s.online);
  const ready = useBoardStore((s) => s.ready);
  const head = useBoardStore((s) => s.head);
  const fetchedAt = useBoardStore((s) => s.fetchedAt);
  const error = useBoardStore((s) => s.error);
  const notice = useBoardStore((s) => s.notice);
  const tools = useBoardStore((s) => s.tools);
  const refresh = useBoardStore((s) => s.refresh);
  const [, tick] = useState(0);

  useEffect(() => {
    const t = window.setInterval(() => tick((n) => n + 1), 10_000);
    return () => window.clearInterval(t);
  }, []);

  if (!ready) return <p className="text-xs text-subtle">공책 읽는 중…</p>;
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      {error ? (
        <div role="alert" className="banner banner-danger">
          <span className="min-w-0 flex-1 truncate">{error}</span>
          <button type="button" className="btn-quiet" onClick={() => void refresh()}>
            다시 해보기
          </button>
        </div>
      ) : null}
      <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-xs">
        <span className={cn("badge", online ? "badge-adopt" : "badge-reject")}>
          {online ? SAY.connected : SAY.offline}
        </span>
        {online ? <span className="font-mono text-subtle">{head}</span> : null}
        {online ? <span className="text-subtle">{ago(fetchedAt)}에 봤어요</span> : null}
        {online ? (
          <span className="flex items-center gap-2 font-mono" title="로봇 열쇠 — 줄이 그어지면 로그인이 풀린 것">
            <span aria-hidden>🔑</span>
            {CLI.map((b) => (
              <span key={b} className={cn(tools[b] ? "text-muted" : "text-reject-fg line-through")}>
                {b}
                {b === "grok" && tools.grok && !tools.grokLogin ? " (열쇠 없음)" : ""}
              </span>
            ))}
          </span>
        ) : null}
        {notice && !error ? (
          <span className="text-muted" role="status">
            {notice}
          </span>
        ) : null}
      </div>
    </div>
  );
}
