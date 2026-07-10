#!/usr/bin/env python3
"""git pull 후 함대 자동 기동 — post-merge 훅이 부른다.

목적: 기계를 옮길 때(윈도우 → 맥) `git pull` 한 번으로 함대가 알아서 뜨게 한다.

**이 스크립트의 진짜 일은 기동이 아니라 거부다.** 이 저장소의 최대 금기는
두 기계가 각자 master에 병합하는 이중 가동이다. 훅이 무심코 그걸 유발할 수 있으므로,
아래 세 관문을 모두 통과할 때만 기동한다:

  1. BOTS_OFF 플래그가 없을 것        — 오너가 의도적으로 내린 기계는 그대로 둔다
                                        (output/cache/* 는 gitignore라 기계별로 독립)
  2. 이 기계가 지정된 운영기일 것      — TELEGRAM_POLL_HOST를 운영기 지정자로 재사용한다.
                                        새 개념을 만들지 않아야 두 곳이 어긋나지 않는다.
  3. 인터프리터에 playwright가 있을 것 — 없으면 봄이·미오가 기동 즉시 죽는다(2026-07-10)

어느 하나라도 막히면 기동하지 않고 이유와 해결 명령을 출력한다.
git pull 자체는 절대 실패시키지 않는다(항상 exit 0).

  python fleet_bootstrap.py                  # 기동 (훅이 부르는 기본 모드)
  python fleet_bootstrap.py --install-hook   # post-merge 훅 활성화 (기계당 1회)
  python fleet_bootstrap.py --claim-ops-host # 이 기계를 운영기로 지정 (.env 재암호화)
  python fleet_bootstrap.py --check          # 관문만 점검, 기동 안 함
"""
import argparse
import os
import platform
import subprocess
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from _shared.env import load_env  # noqa: E402
from _shared.utils import find_root  # noqa: E402

ROOT = find_root()
AI_TEAM = ROOT / "projects" / "ai-team"
CONTROLLER = AI_TEAM / "skills" / "영숙_비서" / "tools" / "agent_controller.py"
BOTS_OFF_FLAG = ROOT / "output" / "cache" / "BOTS_OFF"
ENV_ENC = ROOT / ".env.encrypted"
_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


def _host() -> str:
    return platform.node().strip()


def gates() -> tuple[bool, str]:
    """(기동해도 되는가, 사유). 거부 사유엔 해결 명령을 담는다."""
    if BOTS_OFF_FLAG.exists():
        return False, ("BOTS_OFF 플래그 — 이 기계는 의도적으로 정지 상태다.\n"
                       f"  켜려면: python {CONTROLLER.relative_to(ROOT)} 봇다켜")

    designated = os.getenv("TELEGRAM_POLL_HOST", "").strip()
    if designated and designated.lower() != _host().lower():
        return False, (f"이 기계({_host()})는 지정 운영기({designated})가 아니다 — 이중 가동 방지.\n"
                       f"  이 기계로 옮기려면: python {Path(__file__).relative_to(ROOT)} --claim-ops-host")

    try:
        import playwright  # noqa: F401
    except ImportError:
        return False, (f"이 인터프리터({sys.executable})에 playwright가 없다 — 봄이·미오가 즉사한다.\n"
                       "  playwright 있는 인터프리터로 훅을 다시 설치하라 (--install-hook)")
    return True, f"통과 — 운영기 {_host()}, 인터프리터 {sys.executable}"


def start_fleet() -> int:
    r = subprocess.run([sys.executable, str(CONTROLLER), "봇다켜"], cwd=str(ROOT),
                       capture_output=True, text=True, encoding="utf-8",
                       errors="replace", timeout=300, **_NOWIN)
    print((r.stdout or r.stderr).strip())
    from _shared.notify import agent_status
    status = agent_status()
    down = [k for k, v in status.items() if v in ("down", "misconfig")]
    if down:
        print(f"⚠️  기동 실패: {', '.join(down)} — 로그 확인 필요")
        return 1
    print(f"✅ 함대 {len(status)}종 가동")
    return 0


# 훅은 커밋되므로 기계 중립이어야 한다. 인터프리터 경로는 기계마다 다르니
# gitignore되는 로컬 파일에 적고, 훅이 실행 시점에 읽는다.
# (윈도우 경로를 훅에 박으면 맥에서 그대로 깨진다.)
PY_PIN = ROOT / "output" / "cache" / "BOOTSTRAP_PYTHON"

_HOOK = '''#!/bin/sh
# git pull 후 함대 자동 기동. 관문(BOTS_OFF·운영기·playwright)은 fleet_bootstrap.py가 판단한다.
# pull 자체는 절대 실패시키지 않는다.
ROOT="$(git rev-parse --show-toplevel)"
PIN="$ROOT/output/cache/BOOTSTRAP_PYTHON"
if [ -f "$PIN" ]; then PY="$(cat "$PIN")"; \\
elif command -v python3 >/dev/null 2>&1; then PY=python3; \\
else PY=python; fi
"$PY" "$ROOT/projects/ai-team/scripts/fleet_bootstrap.py" || true
'''


def install_hook() -> int:
    hooks = ROOT / ".githooks"
    hooks.mkdir(exist_ok=True)
    hook = hooks / "post-merge"
    hook.write_text(_HOOK, encoding="utf-8", newline="\n")
    hook.chmod(0o755)
    # 인터프리터는 '이 스크립트를 실행한 파이썬'으로 고정한다 — 훅이 PATH의 다른 venv를
    # 잡아 playwright 없는 파이썬으로 함대를 띄우는 것을 막는다(2026-07-10 교훈).
    PY_PIN.parent.mkdir(parents=True, exist_ok=True)
    PY_PIN.write_text(sys.executable, encoding="utf-8")
    r = subprocess.run(["git", "config", "core.hooksPath", ".githooks"], cwd=str(ROOT),
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(f"❌ core.hooksPath 설정 실패: {r.stderr.strip()}")
        return 1
    print(f"✅ post-merge 훅 활성화 — 이제 git pull 하면 함대가 자동 기동한다.\n"
          f"   인터프리터 고정(이 기계 한정): {sys.executable}")
    return 0


def claim_ops_host() -> int:
    """이 기계를 운영기로 지정한다 — .env.encrypted의 TELEGRAM_POLL_HOST를 갱신."""
    here = _host()
    tmp = ROOT / ".env.claim.tmp"
    env_py = AI_TEAM / "_shared" / "env.py"
    try:
        r = subprocess.run([sys.executable, str(env_py), "decrypt", str(ENV_ENC), str(tmp)],
                           cwd=str(ROOT), capture_output=True, text=True, timeout=60)
        if r.returncode != 0 or not tmp.exists():
            print(f"❌ 복호화 실패: {(r.stderr or r.stdout).strip()[:200]}")
            return 1
        lines = tmp.read_text(encoding="utf-8").splitlines()
        out, found = [], False
        for line in lines:
            if line.strip().startswith("TELEGRAM_POLL_HOST="):
                out.append(f"TELEGRAM_POLL_HOST={here}")
                found = True
            else:
                out.append(line)
        if not found:
            out.append(f"TELEGRAM_POLL_HOST={here}")
        tmp.write_text("\n".join(out) + "\n", encoding="utf-8")
        r = subprocess.run([sys.executable, str(env_py), "encrypt", str(tmp), str(ENV_ENC)],
                           cwd=str(ROOT), capture_output=True, text=True, timeout=60)
        if r.returncode != 0:
            print(f"❌ 재암호화 실패: {(r.stderr or r.stdout).strip()[:200]}")
            return 1
    finally:
        if tmp.exists():
            tmp.unlink()  # 평문 시크릿을 디스크에 남기지 않는다
    print(f"✅ 운영기를 {here}(으)로 지정했다. .env.encrypted가 변경됐다 — 커밋·푸시하라.\n"
          "   다른 기계는 다음 pull 후 기동을 스스로 거부한다.")
    return 0


def setup() -> int:
    """기계를 옮길 때 한 방 — 훅 활성화 + 운영기 지정 + 즉시 기동.

    core.hooksPath는 클론마다 따로라(.git/config) 커밋으로 전파되지 않는다.
    그래서 새 기계에서는 이 명령을 딱 한 번 실행해야 하고, 그 뒤로는 pull만으로 자동이다.
    """
    if install_hook() != 0:
        return 1
    load_env(str(ROOT))
    if claim_ops_host() != 0:
        return 1
    os.environ["TELEGRAM_POLL_HOST"] = _host()  # 방금 지정한 값을 이 프로세스에도 반영
    if BOTS_OFF_FLAG.exists():
        BOTS_OFF_FLAG.unlink()  # 이 기계를 운영기로 삼는다 = 정지 의사 철회
        print("BOTS_OFF 해제")
    ok, why = gates()
    print(f"[부트스트랩] {'기동' if ok else '기동 안 함'} — {why}")
    return start_fleet() if ok else 0


def main() -> int:
    ap = argparse.ArgumentParser(description="git pull 후 함대 자동 기동")
    ap.add_argument("--setup", action="store_true",
                    help="새 기계 전환 한 방: 훅 활성화 + 운영기 지정 + 기동")
    ap.add_argument("--install-hook", action="store_true", help="post-merge 훅 활성화(기계당 1회)")
    ap.add_argument("--claim-ops-host", action="store_true", help="이 기계를 운영기로 지정")
    ap.add_argument("--check", action="store_true", help="관문만 점검")
    args = ap.parse_args()

    if args.setup:
        return setup()
    if args.install_hook:
        return install_hook()

    load_env(str(ROOT))
    if args.claim_ops_host:
        return claim_ops_host()

    ok, why = gates()
    print(f"[부트스트랩] {'기동' if ok else '기동 안 함'} — {why}")
    if args.check or not ok:
        return 0  # git pull을 실패시키지 않는다
    return start_fleet()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as e:  # 훅에서 도는 코드는 어떤 경우에도 pull을 깨면 안 된다
        print(f"[부트스트랩] 예외 무시: {e}")
        sys.exit(0)
