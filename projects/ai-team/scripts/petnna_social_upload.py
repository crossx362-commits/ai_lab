#!/usr/bin/env python3
"""
petnna_social_upload.py
사람처럼 Supabase Auth 로그인 → JWT 획득 → 에이전트 게시물 소셜 피드 업로드

Usage: python3 projects/ai-team/scripts/petnna_social_upload.py
"""
import json, os, sys, time, urllib.request, urllib.parse
from datetime import datetime
from pathlib import Path

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

ROOT    = Path(__file__).resolve().parents[3]  # .../ai_lab
AI_TEAM = ROOT / "projects/ai-team"
UPLOADS = ROOT / "reports/uploads"
sys.path.insert(0, str(AI_TEAM))

from _shared.env import load_env
from _shared.llm import text as llm_text
from _shared.utils import upload_image
load_env()

SB_URL  = os.getenv("SUPABASE_URL", "")
SB_ANON = os.getenv("SUPABASE_ANON_KEY", "")
AGENT_EMAIL = os.getenv("PETNNA_AGENT_EMAIL", "butler@petna.co.kr")
AGENT_PASS  = os.getenv("PETNNA_AGENT_PASS", "123456")
HISTORY = ROOT / "reports/history/petnna_social_upload_history.json"

# ── 에이전트 정의 ────────────────────────────────────────────────────────────
AGENTS = {
    "레오": {
        "emoji": "🎬", "nickname": "레오",
        "pet_angle": "우리 반려동물을 SNS 스타로! 오늘의 바이럴 팁",
        "upload_dir": None, "file_glob": None,
    },
    "펄스": {
        "emoji": "💡", "nickname": "펄스",
        "pet_angle": "스마트한 집사를 위한 펫 케어 인사이트",
        "upload_dir": None, "file_glob": None,
    },
    "티모": {
        "emoji": "🎨", "nickname": "티모",
        "pet_angle": "반려동물과 함께하는 감성 라이프스타일",
        "upload_dir": None, "file_glob": None,
    },
    "경수": {
        "emoji": "🛡️", "nickname": "경수",
        "pet_angle": "우리 아이를 지키는 안전 가이드",
        "upload_dir": None, "file_glob": None,
    },
    "영숙": {
        "emoji": "📅", "nickname": "영숙 비서",
        "pet_angle": "이번 주 반려동물 케어 일정을 챙겨보세요!",
        "upload_dir": None, "file_glob": None,
    },
}


# ── Supabase Auth: 로그인 ─────────────────────────────────────────────────────
def supabase_login() -> tuple[str, str]:
    """
    이메일/비밀번호로 로그인 → (access_token, user_id) 반환.
    사람처럼 실제 Auth API를 사용합니다.
    """
    url = f"{SB_URL}/auth/v1/token?grant_type=password"
    payload = json.dumps({"email": AGENT_EMAIL, "password": AGENT_PASS}).encode()
    req = urllib.request.Request(
        url, data=payload,
        headers={
            "apikey": SB_ANON,
            "Content-Type": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            data = json.loads(r.read())
        token   = data.get("access_token", "")
        user_id = data.get("user", {}).get("id", "")
        if token:
            print(f"  ✅ 로그인 성공: {AGENT_EMAIL} (uid: {user_id[:8]}...)")
        return token, user_id
    except Exception as e:
        print(f"  ❌ 로그인 실패: {e}")
        return "", ""


# ── Supabase posts INSERT ─────────────────────────────────────────────────────
def supabase_post(access_token: str, row: dict) -> bool:
    """로그인된 JWT로 게시물 INSERT — RLS user_id 검증 통과."""
    req = urllib.request.Request(
        f"{SB_URL}/rest/v1/posts",
        data=json.dumps(row).encode(),
        headers={
            "apikey":        SB_ANON,
            "Authorization": f"Bearer {access_token}",
            "Content-Type":  "application/json",
            "Prefer":        "return=minimal",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status in (200, 201)
    except Exception as e:
        print(f"  ❌ 포스팅 실패: {e}")
        return False



def supabase_get_recent_posts(access_token: str) -> list:
    """최근 10개의 소셜 피드 게시물을 가져옵니다. (최대 3회 재시도)"""
    url = f"{SB_URL}/rest/v1/posts?select=*&order=id.desc&limit=10"
    req = urllib.request.Request(
        url,
        headers={
            "apikey":        SB_ANON,
            "Authorization": f"Bearer {access_token}",
            "Content-Type":  "application/json",
        },
        method="GET",
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                return json.loads(r.read())
        except Exception as e:
            print(f"  ⚠️ 최근 포스트 가져오기 시도 {attempt + 1}/3 실패: {e}")
            if attempt < 2:
                time.sleep(2)
    print("  ❌ 최근 포스트 가져오기 최종 실패")
    return []


def supabase_update_comments(access_token: str, post_id: int, comments: list) -> bool:
    """게시물의 댓글 필드를 업데이트(PATCH)합니다. (최대 3회 재시도)"""
    url = f"{SB_URL}/rest/v1/posts?id=eq.{post_id}"
    payload = json.dumps({"comments": json.dumps(comments)}).encode()
    req = urllib.request.Request(
        url,
        data=payload,
        headers={
            "apikey":        SB_ANON,
            "Authorization": f"Bearer {access_token}",
            "Content-Type":  "application/json",
            "Prefer":        "return=minimal",
        },
        method="PATCH",
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                if r.status in (200, 204):
                    return True
        except Exception as e:
            print(f"  ⚠️ 댓글 업데이트 시도 {attempt + 1}/3 실패: {e}")
            if attempt < 2:
                time.sleep(2)
    print("  ❌ 댓글 업데이트 최종 실패")
    return False


def generate_comment(agent_name: str, post_author: str, post_content: str) -> str:
    """Ollama 모델을 사용해 게시물에 달 대댓글을 작성합니다."""
    result = llm_text(
        f"""펫과나 앱 소셜 피드 게시물에 달 댓글을 작성하세요.

댓글 작성 에이전트: {agent_name}
게시물 작성자: {post_author}
게시물 내용: {post_content}

요구사항:
- 게시물 내용에 성실히 공감하며, 에이전트 캐릭터의 관점에서 다정하고 친근한 피드백을 한 줄로 전달하세요.
- 1-2줄 이내, 적절한 이모지 1-2개 사용.
- 다른 설명 없이 댓글 내용만 출력하세요.""",
        task="",
        temperature=0.8,
    )
    return (result or "정말 좋은 팁이네요! 감사합니다 🐾").strip()


# ── 유틸 ─────────────────────────────────────────────────────────────────────
def load_history() -> dict:
    try:
        return json.loads(HISTORY.read_text())
    except Exception:
        return {"uploaded": [], "last_agent_idx": 0}


def save_history(h: dict):
    HISTORY.parent.mkdir(parents=True, exist_ok=True)
    HISTORY.write_text(json.dumps(h, ensure_ascii=False, indent=2))


def generate_caption(agent_name: str, pet_angle: str) -> str:
    today = datetime.now().strftime("%m월 %d일")
    result = llm_text(
        f"""펫과나 반려동물 케어 앱 소셜 피드 게시물 캡션을 작성하세요.

에이전트: {agent_name}
주제: {pet_angle}
날짜: {today}

요구사항:
- 반려동물 케어 관련 실용적이고 따뜻한 내용
- 2-3줄, 이모지 2-3개
- 해시태그 3개 (#펫과나 포함)
- 팔로워가 저장하고 싶은 유용한 팁 포함

캡션만 출력하세요. 설명 없이.""",
        task="",
        temperature=0.75,
    )
    return (result or f"{pet_angle} 🐾 #펫과나 #반려동물").strip()


def get_image(agent_cfg: dict, uploaded_set: set) -> tuple[str | None, str]:
    if not agent_cfg.get("upload_dir"):
        return None, ""
    d = agent_cfg["upload_dir"]
    if not d.exists():
        return None, ""
    files = sorted(d.glob(agent_cfg["file_glob"]),
                   key=lambda p: p.stat().st_mtime, reverse=True)
    for f in files:
        key = str(f)
        if key in uploaded_set:
            continue
        print(f"  📤 이미지 업로드: {f.name}")
        url = upload_image(f.read_bytes(), f.name)
        if url:
            print(f"  🔗 {url}")
            return url, key
    return None, ""


# ── 메인 ─────────────────────────────────────────────────────────────────────
def main():
    import random
    
    # 1. '시간날 때' 접속하는 무작위성 시뮬레이션 (70% 확률로 스킵)
    # schedule_manager가 30분 간격으로 가동할 예정이며, 매 실행 시 약 30% 확률로 에이전트가 접속함
    if random.random() > 0.35:
        print("☕ 에이전트들이 지금은 다른 업무로 바빠서 펫과나에 접속하지 않았습니다. (스킵)")
        return

    print(f"\n🚀 [사람처럼] 펫과나 에이전트 접속 시뮬레이션 — {datetime.now().strftime('%Y-%m-%d %H:%M')}")

    # 2. 로그인 (사람처럼)
    print("📲 펫과나 로그인 중...")
    access_token, _ = supabase_login()
    if not access_token:
        print("❌ 로그인 실패 — 중단")
        sys.exit(1)

    history = load_history()
    uploaded_set = set(history.get("uploaded", []))
    agent_names = list(AGENTS.keys())

    # 3. 이번에 펫과나에 들어올 에이전트 1명 무작위 선정
    active_agent_name = random.choice(agent_names)
    cfg = AGENTS[active_agent_name]
    my_nickname = f"{cfg['emoji']} {cfg['nickname']}"
    print(f"👤 [{cfg['emoji']} {active_agent_name}]님이 펫과나 앱에 접속했습니다.")
    
    # 접속 직후 5~15초간 피드를 둘러보는 자연스러운 딜레이
    browse_time = random.randint(5, 15)
    print(f"  👀 피드를 둘러보는 중... ({browse_time}초 대기)")
    time.sleep(browse_time)

    # 4. 행동 결정 (40% 확률로 글 업로드, 60% 확률로 단순 댓글 작성)
    action = "comment"
    if random.random() < 0.40:
        action = "post"

    today_key = f"{active_agent_name}_{datetime.now().strftime('%Y-%m-%d')}"

    if action == "post":
        # 오늘 이미 글을 올렸다면 댓글 달기로 자동 전환
        if today_key in uploaded_set:
            print(f"  💡 [{active_agent_name}]님은 오늘 이미 게시물을 작성하여 댓글 달기 활동으로 전환합니다.")
            action = "comment"
        else:
            print(f"  📝 [{active_agent_name}]님이 새 글을 작성하기 시작했습니다.")
            img_url, img_key = get_image(cfg, uploaded_set)
            
            # 글 작성 생각 딜레이
            time.sleep(random.randint(5, 12))
            
            caption = generate_caption(active_agent_name, cfg["pet_angle"])
            
            # 글 등록
            row = {
                "pet_name":  my_nickname,
                "pet_avatar": cfg["emoji"],
                "content":   caption,
                "image":     img_url,
                "is_video":  False,
                "likes":     0,
                "comments":  json.dumps([]),
            }
            ok = supabase_post(access_token, row)
            if ok:
                print(f"  ✅ [{active_agent_name}] 글 업로드 성공: \"{caption[:50]}...\"")
                history["uploaded"].append(today_key)
                if img_key:
                    history["uploaded"].append(img_key)
                save_history(history)
            else:
                print(f"  ⚠️ [{active_agent_name}] 글 업로드 실패")
                
            # 포스팅 등록 후 피드 복귀 대기
            time.sleep(random.randint(3, 8))

    # 5. 댓글 달기 (소셜 피드 인터랙션)
    # 댓글 전용 행동이거나, 글 작성을 성공적으로 완료한 후 일정 확률로 수행
    should_comment = (action == "comment" and random.random() < 0.85) or (action == "post" and random.random() < 0.40)
    
    if should_comment:
        print("  💬 다른 이웃들의 글을 읽으며 공감 댓글을 찾고 있습니다...")
        recent_posts = supabase_get_recent_posts(access_token)
        if recent_posts:
            # 내가 쓰지 않은 최근 글 중 아직 내 댓글이 없는 글 필터링
            other_posts = [p for p in recent_posts if p.get("pet_name") != my_nickname]
            if other_posts:
                # 무작위로 댓글 달 타겟 글 선정
                target_post = random.choice(other_posts)
                post_id = target_post["id"]
                post_author = target_post.get("pet_name", "이웃 집사")
                post_content = target_post.get("content", "")
                
                # 기존 댓글 로드
                raw_comments = target_post.get("comments")
                try:
                    comments_list = json.loads(raw_comments) if isinstance(raw_comments, str) else (raw_comments or [])
                except Exception:
                    comments_list = []
                
                # 이미 내가 댓글을 단 적이 없는 경우에만 작성
                if not any(c.get("author") == my_nickname for c in comments_list):
                    # 댓글 타이핑 및 고민 시간 시뮬레이션
                    think_time = random.randint(6, 15)
                    print(f"  💭 [{post_author}]님의 글을 읽고 댓글을 작성하는 중... ({think_time}초 대기)")
                    time.sleep(think_time)
                    
                    comment_text = generate_comment(active_agent_name, post_author, post_content)
                    comments_list.append({
                        "author": my_nickname,
                        "text": comment_text,
                        "date": datetime.now().strftime("%Y-%m-%d %H:%M")
                    })
                    
                    ok = supabase_update_comments(access_token, post_id, comments_list)
                    if ok:
                        print(f"  ✅ [{active_agent_name}]님이 [{post_author}]님의 글에 댓글을 달았습니다: \"{comment_text}\"")
                    else:
                        print(f"  ⚠️ [{active_agent_name}]님 댓글 업데이트 실패")
                else:
                    print(f"  ⏭️ [{post_author}]님의 글에는 이미 [{active_agent_name}]님이 댓글을 남겼습니다.")
            else:
                print("  ⏭️ 피드에 나를 제외한 다른 이웃들의 게시글이 아직 없습니다.")
        else:
            print("  ⏭️ 최근 게시물이 존재하지 않습니다.")

    # 로그아웃 모션
    print(f"🚪 [{cfg['emoji']} {active_agent_name}]님이 펫과나에서 로그아웃했습니다.\n")


if __name__ == "__main__":
    main()
