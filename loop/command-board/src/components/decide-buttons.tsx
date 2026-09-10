import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { useBoardStore } from "@/lib/board-store";
import type { Verdict } from "@/lib/board-types";
import { SAY } from "@/lib/words";

/**
 * 채택/반려(또는 예/아니오) 버튼 한 벌.
 * 실행을 일으키는 쪽(채택·예)은 두 번 눌러 확정한다 — 첫 클릭은 3초간 「진짜? 한 번 더!」로 바뀌고, 그 안에 다시 누르면 보낸다.
 * 반려·아니오는 되돌리기 쉬운 안전한 선택이라 한 번에 보낸다(웹 조사: 기본값은 안전한 쪽, 오클릭 방지).
 * forceArm/onArmChange: 받은편지함의 키보드(a 두 번)와 같은 무장 상태를 공유할 때.
 */
export function DecideButtons({
  id,
  yesNo,
  compact,
  forceArm,
  onArmChange,
}: {
  id: string;
  yesNo?: boolean;
  compact?: boolean;
  forceArm?: boolean;
  onArmChange?: (armed: boolean) => void;
}) {
  const decide = useBoardStore((s) => s.decide);
  const busy = useBoardStore((s) => s.busy);
  const [local, setLocal] = useState(false);
  const arm = forceArm ?? local;
  const setArm = (v: boolean) => {
    setLocal(v);
    onArmChange?.(v);
  };

  useEffect(() => {
    if (!local) return;
    const t = window.setTimeout(() => setLocal(false), 3000);
    return () => window.clearTimeout(t);
  }, [local]);

  const send = (v: Verdict) => void decide(id, v);
  const adoptLabel = yesNo ? SAY.yes : SAY.adopt;
  const rejectLabel = yesNo ? SAY.no : SAY.reject;

  return (
    <div className={compact ? "flex shrink-0 gap-1.5" : "mt-3 grid grid-cols-2 gap-2"} role="group" aria-label="판정">
      <Button
        type="button"
        variant={arm ? "default" : "adopt"}
        size={compact ? "xs" : "sm"}
        disabled={busy}
        aria-pressed={arm}
        onClick={() => {
          if (arm) {
            setArm(false);
            send("채택");
          } else {
            setArm(true);
          }
        }}
      >
        {arm ? "🙌 " + SAY.adoptConfirm : "👍 " + adoptLabel}
      </Button>
      <Button type="button" variant="reject" size={compact ? "xs" : "sm"} disabled={busy} onClick={() => send("반려")}>
        {"👎 " + rejectLabel}
      </Button>
    </div>
  );
}
