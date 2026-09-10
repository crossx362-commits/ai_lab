import { useEffect } from "react";
import { useBoardStore } from "@/lib/board-store";
import { LAB_PROJECTS } from "@/lib/lab-projects";
import { PHASE_KO, statusOf } from "@/lib/lab-status";
import { PROJECT_STATES, type ProjectState } from "@/lib/board-api";
import { cn } from "@/lib/cn";

/** 오너가 정하는 상태 — 색은 의미색 토큰만 쓴다(진행=액센트, 보류=노랑, 완료=초록, 접음=회색) */
const STATE_STYLE: Record<ProjectState, { badge: string; hint: string }> = {
  진행: { badge: "badge-primary", hint: "로봇이 계속 일함" },
  보류: { badge: "badge-warn", hint: "새 일은 멈춤, 하던 것만 마무리" },
  완료: { badge: "badge-adopt", hint: "끝. 손대지 않음" },
  접음: { badge: "badge-mute", hint: "당분간 안 봄" },
};

function since(date: string) {
  if (!date) return "기록 없음";
  const d = Math.round((Date.now() - new Date(date).getTime()) / 86_400_000);
  return d <= 0 ? "오늘" : d === 1 ? "어제" : d + "일 전";
}

/** 모든 프로젝트의 진행 현황(로컬 git) + 오너 상태 버튼. 헤더 「프로젝트 현황」으로 연다. */
export function ProjectPanel() {
  const open = useBoardStore((s) => s.projectPanel);
  const rows = useBoardStore((s) => s.projectGit);
  const states = useBoardStore((s) => s.projectStates);
  const busy = useBoardStore((s) => s.busy);
  const setState = useBoardStore((s) => s.setProjectState);
  const setProject = useBoardStore((s) => s.setProject);
  const active = useBoardStore((s) => s.activeProjectId);
  const load = useBoardStore((s) => s.loadProjectGit);

  useEffect(() => {
    if (!open) return;
    const t = window.setInterval(() => void load(), 60_000);
    return () => window.clearInterval(t);
  }, [open, load]);

  if (!open) return null;
  const byId = new Map(rows.map((r) => [r.id, r]));

  return (
    <section className="shrink-0 border-b border-border bg-elevated px-3 py-3" aria-labelledby="projects-heading">
      <div className="mb-2 flex items-center gap-3">
        <h2 id="projects-heading" className="text-sm font-semibold tracking-tight">
          프로젝트 현황
        </h2>
        <span className="text-xs text-subtle">진행 = 로봇이 일함 · 보류/완료/접음 = 새 일 안 시작. 상태는 오너만 바꿔요.</span>
        {!rows.length ? <span className="text-xs text-subtle">git 기록 읽는 중…</span> : null}
      </div>
      <div className="scroll-quiet grid max-h-[46dvh] grid-cols-1 gap-2 overflow-y-auto pr-1 sm:grid-cols-2 xl:grid-cols-3">
        {LAB_PROJECTS.map((p) => {
          const git = byId.get(p.id);
          const st = states[p.id];
          const phase = statusOf(p.id).phase;
          const hot = (git?.week || 0) > 0;
          return (
            <article
              key={p.id}
              className={cn(
                "flex flex-col gap-2 rounded-lg border border-border bg-raised p-3",
                active === p.id && "border-primary",
                st?.state === "접음" && "opacity-60",
              )}
            >
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setProject(p.id)}
                  className="truncate text-sm font-semibold text-foreground hover:underline"
                  title="이 프로젝트를 명령 대상으로"
                >
                  {p.name}
                </button>
                <span className="badge badge-mute">{PHASE_KO[phase]}</span>
                {st ? <span className={cn("badge", STATE_STYLE[st.state].badge)}>{st.state}</span> : <span className="badge badge-mute">미정</span>}
              </div>
              <p className="min-w-0 truncate text-xs text-muted" title={git?.message || ""}>
                {git ? git.message || "(커밋 메시지 없음)" : "…"}
              </p>
              <div className="flex items-center gap-3 text-[11px] text-subtle">
                <span>마지막 {git ? since(git.date) : "…"}</span>
                <span className={cn("font-mono", hot && "text-foreground")}>이번 주 {git ? git.week : "…"}커밋</span>
                {git?.sha ? <span className="font-mono">{git.sha}</span> : null}
              </div>
              <div className="grid grid-cols-4 gap-1" role="group" aria-label={p.name + " 상태"}>
                {PROJECT_STATES.map((s) => {
                  const on = st?.state === s;
                  return (
                    <button
                      key={s}
                      type="button"
                      disabled={busy || on}
                      title={STATE_STYLE[s].hint}
                      onClick={() => void setState(p.id, s)}
                      className={cn(
                        "h-7 rounded-md border text-xs font-medium transition-colors duration-[var(--motion-quick)] ease-[var(--ease-out)]",
                        on ? "border-primary bg-primary text-primary-foreground" : "border-border bg-elevated text-muted hover:border-border-strong hover:text-foreground",
                        (busy || on) && "cursor-default",
                      )}
                    >
                      {s}
                    </button>
                  );
                })}
              </div>
              {st?.note ? <p className="text-[11px] text-subtle">메모: {st.note}</p> : null}
            </article>
          );
        })}
      </div>
    </section>
  );
}
