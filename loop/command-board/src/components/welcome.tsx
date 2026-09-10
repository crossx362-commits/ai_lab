import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";

const KEY = "command-board:welcome-seen";

/** 첫 방문 3컷 — 당신은 대장 / 로봇 넷이 생각 / 도장 쾅. 한 번 보면 안 뜨고, 「설명서」에서 언제든 다시. */
export function Welcome() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    try {
      if (!window.localStorage.getItem(KEY)) setOpen(true);
    } catch {
      setOpen(true);
    }
  }, []);

  if (!open) return null;
  const close = () => {
    try {
      window.localStorage.setItem(KEY, "1");
    } catch {
      /* 저장 못 하면 다음에 또 보여줘도 괜찮다 */
    }
    setOpen(false);
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="welcome-title"
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/30 p-4"
    >
      <div className="w-full max-w-xl rounded-[var(--radius-xl)] border-2 border-border bg-raised p-6 shadow-[var(--shadow-sticker-lg)]">
        <h2 id="welcome-title" className="font-display text-3xl">
          🎪 여기는 지휘 보드예요
        </h2>
        <p className="mt-1 text-sm text-muted">당신은 대장. 말하고, 도장만 찍으면 돼요. 심부름은 로봇 몫.</p>
        <ol className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-3">
          <li className="rounded-[var(--radius-lg)] bg-sky-soft p-4">
            <div className="text-3xl" aria-hidden>
              📣
            </div>
            <p className="mt-2 font-display text-base">1. 한 줄 시키기</p>
            <p className="mt-1 text-xs text-muted">맨 위 칸에 적고 「시키기!」</p>
          </li>
          <li className="rounded-[var(--radius-lg)] bg-lav-soft p-4">
            <div className="text-3xl" aria-hidden>
              🦊🐘🐬🐻
            </div>
            <p className="mt-2 font-display text-base">2. 로봇 넷이 생각</p>
            <p className="mt-1 text-xs text-muted">몇 분 뒤 생각이 카드로 와요</p>
          </li>
          <li className="rounded-[var(--radius-lg)] bg-mint-soft p-4">
            <div className="text-3xl" aria-hidden>
              ✅
            </div>
            <p className="mt-2 font-display text-base">3. 도장 쾅</p>
            <p className="mt-1 text-xs text-muted">「좋아, 해!」는 두 번, 「아니야」는 한 번</p>
          </li>
        </ol>
        <div className="mt-5 flex items-center justify-between gap-3">
          <a href="/eli5.html" target="_blank" rel="noreferrer" className="text-sm text-primary underline-offset-2 hover:underline">
            그림 설명서 보기
          </a>
          <Button type="button" size="lg" onClick={close} autoFocus>
            알았어요, 시작!
          </Button>
        </div>
      </div>
    </div>
  );
}
