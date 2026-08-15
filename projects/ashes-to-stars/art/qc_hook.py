#!/usr/bin/env python3
"""
PostToolUse 훅 — 에셋을 생성한 Bash 명령 뒤에 자동으로 검수를 돌린다.

왜 훅인가:
  검수는 "하기로 정해둔 것"이지 "하게 되는 것"이 아니다. 문서 §9에 7항을 적어놔도
  사람이 32장을 매번 눈으로 보지 않으면 없는 규칙이 된다. 생성 직후에 기계가
  자동으로 재고 결과를 대화에 밀어 넣으면 안 볼 수가 없다.

왜 `if` 필터가 아니라 여기서 판정하는가:
  훅 설정의 `if`는 접두 일치라 `cd ... && python3 aigen.py`처럼 앞에 뭐가 붙으면
  안 걸린다. 명령 문자열을 여기서 직접 보는 편이 호출 형태에 안 휘둘린다.

출력: 지적이 있을 때만 additionalContext로 돌려준다. 조용한 성공은 조용히 넘긴다.
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "out_ai")


def main():
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0

    cmd = (payload.get("tool_input") or {}).get("command", "") or ""
    if "aigen.py" not in cmd:
        return 0                     # 에셋 생성이 아니면 아무것도 하지 않는다
    if not os.path.isdir(OUT_DIR):
        return 0

    try:
        p = subprocess.run([sys.executable, os.path.join(HERE, "qc.py"), OUT_DIR, "--json"],
                           capture_output=True, text=True, encoding="utf-8", timeout=120)
        data = json.loads(p.stdout.strip() or "{}")
    except Exception as e:
        # 검수기가 깨졌다는 사실 자체가 알려져야 한다 — 조용히 통과시키지 않는다
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": f"[생성물 검수] 검수기 실행 실패: {e}"}}, ensure_ascii=False))
        return 0

    flagged = data.get("rows") or []
    missing = data.get("missing_required") or []
    if not flagged and not missing:
        return 0                     # 지적 없음 — 대화를 어지럽히지 않는다

    lines = [f"[생성물 검수] {data.get('total', 0)}장 중 지적 {len(flagged)}장"]
    for r in flagged[:12]:
        lines.append(f"  ⚠️ {r['name']} ({r['size']}) — {' · '.join(r['flags'])}")
    if len(flagged) > 12:
        lines.append(f"  … 외 {len(flagged)-12}장")
    if missing:
        lines.append(f"  ❌ 코드가 요구하는데 없는 이름 {len(missing)}종: {', '.join(missing[:10])}"
                     + (" …" if len(missing) > 10 else ""))
    lines.append("  (판정은 사람 몫 — 기계는 실측만 말한다)")

    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": "\n".join(lines)}}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
