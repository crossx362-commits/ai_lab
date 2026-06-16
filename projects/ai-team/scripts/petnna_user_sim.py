#!/usr/bin/env python3
"""
petnna_user_sim.py
에이전트들이 진짜 유저처럼 펫과나를 이용:
- 로그인 → 피드 읽기 → 좋아요 → 댓글 → 게시물 작성 → 펫 데이터 업데이트

Usage: python3 projects/ai-team/scripts/petnna_user_sim.py
"""
import json, os, sys, time, random, urllib.request, urllib.error
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

ROOT    = Path(__file__).resolve().parents[3]   # scripts/→ai-team/→projects/→ai_lab/
AI_TEAM = ROOT / "projects/ai-team"
sys.path.insert(0, str(AI_TEAM))
from _shared.env_loader import load_env
from _shared import ollama_client as lm
load_env()

SB_URL  = os.getenv("SUPABASE_URL", "")
SB_ANON = os.getenv("SUPABASE_ANON_KEY", "")

# ── 에이전트 유저 페르소나 ────────────────────────────────────────────────────
AGENT_USERS = [
    {"name": "레오",    "email": "leo@petna.co.kr",     "pet_name": "레오짱",  "pet_type": "dog",    "emoji": "🎬", "style": "활발하고 마케팅적"},
    {"name": "현빈",    "email": "hyunbin@petna.co.kr", "pet_name": "현빈이",  "pet_type": "dog",    "emoji": "💡", "style": "분석적이고 전략적"},
    {"name": "티모",    "email": "timo@petna.co.kr",    "pet_name": "티모냥",  "pet_type": "cat",    "emoji": "🎨", "style": "감각적이고 디자인 중시"},
    {"name": "경수",    "email": "kyungsoo@petna.co.kr","pet_name": "경수당",  "pet_type": "dog",    "emoji": "🛡️", "style": "신중하고 안전 중시"},
    {"name": "영숙",    "email": "yeongsuk@petna.co.kr","pet_name": "영숙이",  "pet_type": "hamster","emoji": "📅", "style": "부지런하고 일정 관리"},
]

LOG_FILE = ROOT / "reports/history/petnna_user_sim_log.md"


def sb_request(method: str, path: str, token: str, body: dict = None) -> tuple[int, dict]:
    url = f"{SB_URL}/rest/v1/{path}"
    data = json.dumps(body).encode() if body else None
    req = urllib.request.Request(
        url, data=data, method=method,
        headers={
            "apikey":        SB_ANON,
            "Authorization": f"Bearer {token}",
            "Content-Type":  "application/json",
            "Prefer":        "return=representation",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=12) as r:
            return r.status, json.loads(r.read() or b"[]")
    except urllib.error.HTTPError as e:
        return e.code, {}
    except Exception:
        return 0, {}


def login(email: str, password: str = "petna_agent_2026!") -> tuple[str, str]:
    """Supabase Auth 로그인 → (token, user_id)"""
    req = urllib.request.Request(
        f"{SB_URL}/auth/v1/token?grant_type=password",
        data=json.dumps({"email": email, "password": password}).encode(),
        headers={"apikey": SB_ANON, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=12) as r:
            d = json.loads(r.read())
        return d.get("access_token", ""), d.get("user", {}).get("id", "")
    except Exception:
        return "", ""


def signup_if_needed(email: str) -> bool:
    """계정 없으면 가입"""
    req = urllib.request.Request(
        f"{SB_URL}/auth/v1/signup",
        data=json.dumps({"email": email, "password": "petna_agent_2026!"}).encode(),
        headers={"apikey": SB_ANON, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=12) as r:
            d = json.loads(r.read())
        return "id" in d.get("user", {})
    except Exception:
        return False


def get_recent_posts(token: str, limit: int = 10) -> list:
    _, data = sb_request("GET", f"posts?order=created_at.desc&limit={limit}", token)
    return data if isinstance(data, list) else []


def like_post(token: str, post_id: int, current_likes: int):
    sb_request("PATCH", f"posts?id=eq.{post_id}",
               token, {"likes": current_likes + 1})


def comment_on_post(token: str, post_id: int, existing_comments: list, comment_text: str):
    comments = existing_comments + [{"author": "에이전트", "text": comment_text, "ts": datetime.now().isoformat()}]
    sb_request("PATCH", f"posts?id=eq.{post_id}", token, {"comments": json.dumps(comments)})


def write_post(token: str, agent: dict, content: str):
    sb_request("POST", "posts", token, {
        "pet_name":  f"{agent['emoji']} {agent['name']}",
        "pet_avatar": agent["emoji"],
        "content":   content,
        "is_video":  False,
        "likes":     0,
        "comments":  json.dumps([]),
    })


def simulate_session(agent: dict) -> dict:
    """한 에이전트의 앱 이용 세션 시뮬레이션"""
    name  = agent["name"]
    email = agent["email"]
    log   = {"agent": name, "time": datetime.now().isoformat(), "actions": []}

    # 1. 로그인 (계정 없으면 가입)
    token, uid = login(email)
    if not token:
        signup_if_needed(email)
        token, uid = login(email)
    if not token:
        log["actions"].append("❌ 로그인 실패")
        return log
    log["actions"].append(f"✅ 로그인")

    # 2. 피드 읽기
    posts = get_recent_posts(token, limit=5)
    log["actions"].append(f"📖 피드 {len(posts)}개 읽음")

    # 3. 랜덤 좋아요 (50% 확률)
    liked = 0
    for post in posts[:3]:
        if random.random() > 0.5 and post.get("id"):
            like_post(token, post["id"], post.get("likes", 0))
            liked += 1
    if liked:
        log["actions"].append(f"❤️  좋아요 {liked}개")

    # 4. 댓글 (25% 확률, Ollama로 생성)
    for post in posts[:2]:
        if random.random() > 0.75 and post.get("id"):
            context = post.get("content", "")[:100]
            cmt = lm.chat(
                f"펫과나 소셜 피드 댓글을 한 줄로 써주세요. 게시물: '{context}'. "
                f"작성자 성향: {agent['style']}. 댓글만 출력.",
                task="", temperature=0.8,
            )
            if cmt:
                existing = json.loads(post.get("comments") or "[]")
                comment_on_post(token, post["id"], existing, cmt.strip()[:80])
                log["actions"].append(f"💬 댓글: {cmt[:50]}...")

    # 5. 새 게시물 작성 (50% 확률)
    if random.random() > 0.5:
        content = lm.chat(
            f"펫과나 반려동물 소셜 피드 게시물을 2줄로 써주세요. "
            f"에이전트: {name} ({agent['style']}). 반려동물: {agent['pet_name']}({agent['pet_type']}). "
            f"이모지 1-2개, 해시태그 2개 포함. 게시물만 출력.",
            task="", temperature=0.85,
        )
        if content:
            write_post(token, agent, content.strip()[:200])
            log["actions"].append(f"📝 게시물 작성")

    return log


def main():
    print(f"\n🎮 펫과나 유저 시뮬레이션 — {datetime.now().strftime('%Y-%m-%d %H:%M')}")

    if not lm.is_available():
        print("⚠️  Ollama 미실행")

    # 이번 배치: 전체 7명
    batch = AGENT_USERS[:]
    random.shuffle(batch)
    session_logs = []

    for agent in batch:
        print(f"\n[{agent['emoji']} {agent['name']}] 세션 시작...")
        log = simulate_session(agent)
        for action in log["actions"]:
            print(f"  {action}")
        session_logs.append(log)
        time.sleep(2)

    # 로그 저장
    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(f"\n\n## {datetime.now().strftime('%Y-%m-%d %H:%M')} 유저 시뮬레이션\n")
        for log in session_logs:
            f.write(f"- **{log['agent']}**: {' | '.join(log['actions'])}\n")

    posts_created = sum(1 for log in session_logs if any("게시물 작성" in a for a in log["actions"]))
    print(f"\n✅ 완료 — {len(batch)}명 세션, 게시물 {posts_created}개 작성")


if __name__ == "__main__":
    main()
