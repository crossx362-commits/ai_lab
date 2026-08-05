#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""스레드(Meta Threads) 게시 도구 — 클로드가 쓴 글을 Threads API로 발행.

게시는 2단계다: 컨테이너 생성(/threads) → 발행(/threads_publish).
컨테이너만 만드는 건 공개되지 않으므로 --dry-run의 안전한 검증 지점이다.

실행:
  --text "본문"        직접 쓴 본문 게시
  --topic "주제"       클로드가 본문 생성 후 게시
  --dry-run            컨테이너까지만(발행 안 함) — 본문·API 확인용
  --refresh            장기 토큰 갱신(60일 만료 전, .env는 직접 갱신해야 함)

토큰: .env의 THREADS_USER_ID / THREADS_ACCESS_TOKEN.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402

load_env(str(PROJECT_ROOT))

API = "https://graph.threads.net/v1.0"
MAX_LEN = 500  # Threads 텍스트 게시 상한


def _creds() -> tuple[str, str]:
    uid = os.getenv("THREADS_USER_ID", "").strip()
    tok = os.getenv("THREADS_ACCESS_TOKEN", "").strip()
    if not uid or not tok:
        sys.exit("❌ .env에 THREADS_USER_ID / THREADS_ACCESS_TOKEN이 없다")
    return uid, tok


def _call(url: str, data: dict | None = None) -> dict:
    """POST(data 있음) 또는 GET. 오류 본문을 그대로 보여준다(원인 가시화)."""
    try:
        if data is None:
            req = urllib.request.Request(url)
        else:
            req = urllib.request.Request(url, data=urllib.parse.urlencode(data).encode())
        with urllib.request.urlopen(req, timeout=30) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")[:500]
        sys.exit(f"❌ Threads API {e.code}: {body}")
    except Exception as e:
        sys.exit(f"❌ Threads API 호출 실패: {e}")


def generate(topic: str) -> str:
    """클로드/로컬 LLM으로 본문 생성. 실패하면 종료 — 빈 글을 올리지 않는다."""
    from _shared.llm import text as llm_text

    prompt = (
        f"'{topic}'에 대해 스레드(Threads)에 올릴 짧은 글을 한국어로 써라.\n"
        f"조건: {MAX_LEN}자 이내(짧을수록 좋음), 해시태그 0~2개, 링크·광고 없음, "
        "자연스러운 구어체, 설명문 없이 본문만 출력. "
        "Threads는 마크다운을 렌더하지 않으니 **굵게**·# 제목 같은 문법은 쓰지 마라."
    )
    # lm_first=False → 구독 클로드가 1선(로컬은 최후 폴백). 글의 품질이 곧 결과물이라 클라우드 우선.
    out = (llm_text(prompt, lm_first=False, task="blog") or "").strip()
    if not out:
        sys.exit("❌ 본문 생성 실패(LLM 빈 응답)")
    return out[:MAX_LEN]


def publish(body: str, dry_run: bool) -> None:
    uid, tok = _creds()
    if len(body) > MAX_LEN:
        sys.exit(f"❌ 본문 {len(body)}자 — 상한 {MAX_LEN}자 초과")

    print(f"── 본문({len(body)}자) ──\n{body}\n──────────────")

    cid = _call(
        f"{API}/{uid}/threads",
        {"media_type": "TEXT", "text": body, "access_token": tok},
    ).get("id")
    if not cid:
        sys.exit("❌ 컨테이너 id 없음")
    print(f"✅ 컨테이너 생성: {cid}")

    if dry_run:
        print("🛑 --dry-run — 발행하지 않음(컨테이너는 공개되지 않는다)")
        return

    pid = _call(
        f"{API}/{uid}/threads_publish",
        {"creation_id": cid, "access_token": tok},
    ).get("id")
    link = _call(
        f"{API}/{pid}?" + urllib.parse.urlencode({"fields": "permalink", "access_token": tok})
    ).get("permalink", "")
    print(f"🚀 발행 완료: {pid}\n   {link}")


def refresh() -> None:
    """장기 토큰(60일) 갱신. 새 토큰을 .env에 직접 반영해야 한다."""
    _, tok = _creds()
    r = _call(
        f"{API.rsplit('/v1.0', 1)[0]}/refresh_access_token?"
        + urllib.parse.urlencode({"grant_type": "th_refresh_token", "access_token": tok})
    )
    days = int(r.get("expires_in", 0)) // 86400
    print(f"✅ 갱신됨 — 유효기간 {days}일. .env의 THREADS_ACCESS_TOKEN을 아래 값으로 교체할 것:")
    print(r.get("access_token", ""))


def main() -> None:
    ap = argparse.ArgumentParser(description="스레드(Meta Threads) 게시")
    ap.add_argument("--text", help="게시할 본문")
    ap.add_argument("--topic", help="주제 — 클로드가 본문 생성")
    ap.add_argument("--dry-run", action="store_true", help="컨테이너까지만, 발행 안 함")
    ap.add_argument("--refresh", action="store_true", help="장기 토큰 갱신")
    a = ap.parse_args()

    if a.refresh:
        refresh()
        return
    body = a.text or (generate(a.topic) if a.topic else None)
    if not body:
        ap.error("--text 또는 --topic 중 하나가 필요하다")
    publish(body, a.dry_run)


if __name__ == "__main__":
    main()
