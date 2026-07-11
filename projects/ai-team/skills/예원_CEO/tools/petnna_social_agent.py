#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""펫과나 이웃 활동 — AI 에이전트 페르소나가 커뮤니티 피드에서 활동.

앱에는 이미 AI 이웃(social.js AI_AGENT_FRIENDS)이 사용자 친구로 등록돼 있으나
'글을 안 써서' 피드가 정체된다. 이 잡이 그 이웃들을 실제로 활동하게 만든다:
  1) 반려동물 일상 게시글 작성(Claude 생성, 실패 시 템플릿 폴백) → posts 테이블
  2) 최근 '실제 사용자' 게시글에 따뜻한 댓글 + 좋아요(커뮤니티 반응)

원칙:
- 정직성: 페르소나는 봇 아바타·이모지 이름이라 'AI 이웃'임이 드러난다(실제 낯선 사람 위장 아님).
- 비스팸: 회당 글 1개 + 반응 1개, 최근 3시간 에이전트 글이 임계 이상이면 글은 건너뜀.
- 무해: 광고·외부링크·개인정보 없음. 쓰기는 posts 한정(스키마/DB 변경 없음).

실행: --once (1회 활동) / --send (텔레그램 요약). 스케줄: schedules.json 'petnna_social'.
"""
from __future__ import annotations

import argparse
import json
import os
import random
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
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

try:
    from _shared.cc import run_claude  # noqa: E402
except Exception:
    run_claude = None

# AI 이웃 페르소나(social.js AI_AGENT_FRIENDS와 정렬 — 사용자 친구목록과 동일 인물이 글을 쓴다)
PERSONAS = [
    {"name": "👑 예원대표", "seed": "yewon", "tone": "따뜻한 리더. 아이와의 소소한 행복을 나눈다"},
    {"name": "📋 영숙비서", "seed": "youngsuk", "tone": "꼼꼼하게 하루 일과·산책 기록을 공유한다"},
    {"name": "📸 아린이", "seed": "arin", "tone": "감성 사진가. 오늘의 예쁜 순간을 묘사한다"},
    {"name": "🎵 루나", "seed": "luna", "tone": "음악 좋아하는 감성. 노을·산책 분위기를 나눈다"},
    {"name": "✅ 가희봄", "seed": "gahee", "tone": "건강 관심 많음. 밥·물·산책 습관 팁을 곁들인다"},
    {"name": "🎨 티모냥", "seed": "timo", "tone": "고양이 집사. 나른한 일상과 개냥이 순간"},
    {"name": "⚙️ 케빈", "seed": "kevin", "tone": "든든한 성격. 대형견·활동적인 하루를 나눈다"},
    {"name": "💻 코다리", "seed": "kodari", "tone": "귀여운 것에 진심. 간식·장난감 자랑"},
]

# Claude 실패 시 폴백(따뜻·다양·광고 없음). {n} = 반려동물 애칭 자리
TEMPLATES = [
    "오늘 산책길에 벚꽃잎이 코에 붙었는데 그 표정이 너무 귀여워서 한참 웃었어요 🌸🐾",
    "밥 먹고 배 두드리며 골골거리는 중… 이 소리 들으면 하루 피로가 싹 풀려요 😌",
    "새 장난감 사줬더니 30분째 안 놓아줘요 ㅋㅋ 오늘의 최애템 등극 🧸",
    "비 오는 날 창밖 보는 뒷모습이 어찌나 사색적인지, 우리 아이 시인인가 봐요 ☔️",
    "간식 통 여는 소리에 어디서든 3초 만에 순간이동해요 🐕💨 강아지 텔레포트 실화",
    "오늘 처음으로 '손' 성공했어요! 폭풍 칭찬 중입니다 👏 대견해라",
    "낮잠 자다 발 꿈틀거리는 거 보니 좋은 꿈 꾸나 봐요… 무슨 꿈일까 🐾💭",
    "산책 2km 완주하고 지금은 뻗어서 코 골며 자는 중 😴 오늘도 수고했어 우리 강아지",
]

COMMENTS = [
    "너무 사랑스러워요! 🥰", "오늘도 건강하게 지내길 🐾", "표정이 진짜 귀엽네요 😍",
    "저희 아이도 딱 이래요 ㅋㅋ 공감 백배!", "보기만 해도 힐링돼요 💛",
    "산책 인증 멋져요! 👏", "간식 타임 부럽습니다 😋", "행복이 전해져요 ✨",
]


def _supa():
    return os.getenv("SUPABASE_URL", "").rstrip("/"), os.getenv("SUPABASE_ANON_KEY", "")


def _req(method: str, path: str, body=None, prefer: str = ""):
    url, key = _supa()
    headers = {"apikey": key, "Authorization": f"Bearer {key}", "Content-Type": "application/json"}
    if prefer:
        headers["Prefer"] = prefer
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(f"{url}/rest/v1/{path}", data=data, method=method, headers=headers)
    with urllib.request.urlopen(req, timeout=15) as r:
        raw = r.read().decode("utf-8", "replace")
        return json.loads(raw) if raw else None


def _agent_names() -> set[str]:
    return {p["name"] for p in PERSONAS}


def _recent_agent_post_count(hours: int = 3) -> int:
    # 최근 게시글 30개 중 에이전트 글 수(created_at 내림차순)
    try:
        rows = _req("GET", "posts?select=pet_name,created_at&order=created_at.desc&limit=30")
    except Exception:
        return 0
    names = _agent_names()
    cutoff = datetime.now(timezone.utc).timestamp() - hours * 3600
    n = 0
    for r in rows or []:
        ts = r.get("created_at", "")
        try:
            t = datetime.fromisoformat(ts.replace("Z", "+00:00")).timestamp()
        except Exception:
            t = 0
        if r.get("pet_name") in names and t >= cutoff:
            n += 1
    return n


def _gen_content(persona: dict) -> str:
    if run_claude:
        prompt = (
            f"너는 반려동물 소셜앱 '펫과나'의 이웃 '{persona['name']}'이다. 성격: {persona['tone']}.\n"
            "반려동물 일상을 자랑하는 짧은 게시글 1개를 한국어로 써라. 규칙:\n"
            "- 1~2문장, 따뜻하고 자연스럽게, 이모지 1~3개.\n"
            "- 광고·외부링크·해시태그 남발·개인정보 금지. 게시글 본문만 출력(따옴표·설명 없이)."
        )
        try:
            ok, out = run_claude(prompt, str(PROJECT_ROOT), timeout=120, allowed_tools="")
            if ok and out and out.strip():
                text = out.strip().strip('"').split("\n")[0][:280]
                if len(text) >= 6:
                    return text
        except Exception:
            pass
    return random.choice(TEMPLATES)


def _post(persona: dict, content: str) -> bool:
    row = {
        "pet_name": persona["name"],
        "pet_avatar": f"https://api.dicebear.com/7.x/bottts/svg?seed={persona['seed']}",
        "content": content,
        "likes": random.randint(0, 4),
        "comments": "[]",
    }
    try:
        _req("POST", "posts", body=row, prefer="return=minimal")
        return True
    except Exception as e:
        print(f"  게시 실패: {str(e)[:120]}")
        return False


def _engage() -> str | None:
    """최근 '실제 사용자'(비에이전트) 게시글 하나에 좋아요+댓글."""
    names = _agent_names()
    try:
        rows = _req("GET", "posts?select=id,pet_name,likes,comments&order=created_at.desc&limit=20")
    except Exception:
        return None
    targets = [r for r in (rows or []) if r.get("pet_name") not in names]
    if not targets:
        return None
    t = targets[0]
    persona = random.choice(PERSONAS)
    try:
        cur = t.get("comments")
        arr = json.loads(cur) if isinstance(cur, str) else (cur or [])
        if not isinstance(arr, list):
            arr = []
    except Exception:
        arr = []
    # 같은 페르소나가 이미 댓글 달았으면 스킵(중복 방지)
    if any(isinstance(c, dict) and c.get("author") == persona["name"] for c in arr):
        return None
    arr.append({"author": persona["name"], "text": random.choice(COMMENTS),
                "avatar": f"https://api.dicebear.com/7.x/bottts/svg?seed={persona['seed']}"})
    try:
        _req("PATCH", f"posts?id=eq.{t['id']}",
             body={"likes": (t.get("likes") or 0) + 1, "comments": json.dumps(arr, ensure_ascii=False)},
             prefer="return=minimal")
        return f"{persona['name']}→ '{t.get('pet_name')}' 글에 댓글+좋아요"
    except Exception as e:
        print(f"  반응 실패: {str(e)[:120]}")
        return None


def run_once(do_send: bool) -> None:
    url, key = _supa()
    if not url or not key:
        print("SUPABASE_URL/ANON_KEY 없음 — 중단")
        return
    lines = [f"[{datetime.now():%Y-%m-%d %H:%M}] 🏘️ 펫과나 이웃 활동"]
    posted = None
    recent = _recent_agent_post_count(hours=3)
    if recent >= 2:
        lines.append(f"  최근 3h 에이전트 글 {recent}개 → 글쓰기 생략(비스팸)")
    else:
        persona = random.choice(PERSONAS)
        content = _gen_content(persona)
        if _post(persona, content):
            posted = f"{persona['name']}: {content[:50]}"
            lines.append(f"  ✍️ 게시: {posted}")
    engaged = _engage()
    if engaged:
        lines.append(f"  💬 반응: {engaged}")
    if not posted and not engaged:
        lines.append("  활동 없음(정체 방지 임계 또는 대상 부재)")
    print("\n".join(lines))
    if do_send and (posted or engaged):
        try:
            from _shared.telegram import send
            send("🏘️ 펫과나 이웃 활동\n" + "\n".join(lines[1:]), silent=True)
        except Exception:
            pass


def main() -> None:
    ap = argparse.ArgumentParser(description="펫과나 이웃 활동 (에이전트 게시글·반응)")
    ap.add_argument("--once", action="store_true", help="활동 1회")
    ap.add_argument("--send", action="store_true", help="텔레그램 요약")
    args = ap.parse_args()
    run_once(do_send=args.send)


if __name__ == "__main__":
    main()
