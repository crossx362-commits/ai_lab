#!/usr/bin/env python3
"""
petnna_social_upload.py
사람처럼 Supabase Auth 로그인 → JWT 획득 → 에이전트 게시물 소셜 피드 업로드

Usage: cd /Users/junholee/ai_lab && python3 projects/ai-team/scripts/petnna_social_upload.py
"""
import json, os, sys, time, urllib.request, urllib.parse
from datetime import datetime
from pathlib import Path

ROOT    = Path("/Users/junholee/ai_lab")
AI_TEAM = ROOT / "projects/ai-team"
UPLOADS = ROOT / "reports/uploads"
sys.path.insert(0, str(AI_TEAM))

from _shared.env_loader import load_env
from _shared import ollama_client as lm
from _shared.image_uploader import upload_image
load_env()

SB_URL  = os.getenv("SUPABASE_URL", "")
SB_ANON = os.getenv("SUPABASE_ANON_KEY", "")
AGENT_EMAIL = os.getenv("PETNNA_AGENT_EMAIL", "butler@petna.co.kr")
AGENT_PASS  = os.getenv("PETNNA_AGENT_PASS", "123456")
HISTORY = ROOT / "reports/history/petnna_social_upload_history.json"

# ── 에이전트 정의 ────────────────────────────────────────────────────────────
AGENTS = {
    "루나": {
        "emoji": "🎵", "nickname": "루나 디렉터",
        "pet_angle": "AI 시티팝과 함께하는 반려동물 힐링 타임",
        "upload_dir": UPLOADS / "luna", "file_glob": "best_scene_thumbnail*.png",
    },
    "아린": {
        "emoji": "📸", "nickname": "아린 디렉터",
        "pet_angle": "반려동물 일상의 감성적인 순간",
        "upload_dir": UPLOADS / "arin", "file_glob": "*.png",
    },
    "레오": {
        "emoji": "🎬", "nickname": "레오",
        "pet_angle": "우리 반려동물을 SNS 스타로! 오늘의 바이럴 팁",
        "upload_dir": None, "file_glob": None,
    },
    "현빈": {
        "emoji": "💡", "nickname": "현빈",
        "pet_angle": "스마트한 집사를 위한 펫 케어 인사이트",
        "upload_dir": None, "file_glob": None,
    },
    "티모": {
        "emoji": "🎨", "nickname": "티모",
        "pet_angle": "반려동물과 함께하는 감성 라이프스타일",
        "upload_dir": None, "file_glob": None,
    },
    "가희": {
        "emoji": "🏥", "nickname": "가희",
        "pet_angle": "오늘 꼭 확인해야 할 반려동물 건강 체크리스트",
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
    """Ollama/Gemini 모델을 사용해 게시물에 달 대댓글을 작성합니다."""
    result = lm.chat(
        prompt=f"""펫과나 앱 소셜 피드 게시물에 달 댓글을 작성하세요.
        
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
    result = lm.chat(
        prompt=f"""펫과나 반려동물 케어 앱 소셜 피드 게시물 캡션을 작성하세요.

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
    if not lm.is_available():
        print("⚠️  Ollama 미실행 — Gemini 폴백 시도...")

    print(f"\n🚀 펫과나 에이전트 소셜 업로드 — {datetime.now().strftime('%Y-%m-%d %H:%M')}")

    # 1. 로그인 (사람처럼)
    print("\n📲 펫과나 로그인 중...")
    access_token, _ = supabase_login()
    if not access_token:
        print("❌ 로그인 실패 — 중단")
        sys.exit(1)

    history = load_history()
    uploaded_set = set(history.get("uploaded", []))
    agent_names = list(AGENTS.keys())

    # 2. 로테이션 배치 (3개씩)
    idx = history.get("last_agent_idx", 0) % len(agent_names)
    batch = agent_names[idx : idx + 3]
    history["last_agent_idx"] = (idx + 3) % len(agent_names)
    print(f"📋 이번 배치: {', '.join(batch)}\n")

    success = 0
    for name in batch:
        print(f"\n{'='*50}")
        cfg = AGENTS[name]
        today_key = f"{name}_{datetime.now().strftime('%Y-%m-%d')}"

        if today_key in uploaded_set:
            print(f"[{cfg['emoji']} {name}] ⏭️  오늘 이미 업로드됨")
            continue

        print(f"[{cfg['emoji']} {name}] {cfg['pet_angle']}")

        # 이미지
        img_url, img_key = get_image(cfg, uploaded_set)

        # 캡션
        print(f"  🤖 캡션 생성 중...")
        caption = generate_caption(name, cfg["pet_angle"])
        print(f"  📝 {caption[:80]}...")

        # 포스팅 (로그인된 JWT 사용 — user_id는 auth.uid() 기본값)
        row = {
            "pet_name":  f"{cfg['emoji']} {cfg['nickname']}",
            "pet_avatar": cfg["emoji"],
            "content":   caption,
            "image":     img_url,
            "is_video":  False,
            "likes":     0,
            "comments":  json.dumps([]),
        }
        ok = supabase_post(access_token, row)
        print(f"  {'✅ 포스팅 완료' if ok else '⚠️  포스팅 실패'}")

        if ok:
            history["uploaded"].append(today_key)
            if img_key:
                history["uploaded"].append(img_key)
            success += 1

        time.sleep(2)

    # 3. 댓글 달기 (피드백 인터랙션)
    print("\n💬 이웃 피드 댓글 인터랙션 진행 중...")
    recent_posts = supabase_get_recent_posts(access_token)
    if recent_posts:
        for name in batch:
            # 50% 확률로 댓글 작성
            import random
            if random.random() > 0.5:
                continue
            
            cfg = AGENTS[name]
            my_nickname = f"{cfg['emoji']} {cfg['nickname']}"
            
            # 내가 쓰지 않은 최근 글 찾기
            other_posts = [p for p in recent_posts if p.get("pet_name") != my_nickname]
            if not other_posts:
                continue
                
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
                
            # 이미 내가 댓글을 달았는지 확인
            if any(c.get("author") == my_nickname for c in comments_list):
                continue
                
            print(f"  💬 [{cfg['emoji']} {name}]가 [{post_author}]의 글에 댓글 작성 중...")
            comment_text = generate_comment(name, post_author, post_content)
            
            comments_list.append({
                "author": my_nickname,
                "text": comment_text,
                "date": datetime.now().strftime("%Y-%m-%d %H:%M")
            })
            
            ok = supabase_update_comments(access_token, post_id, comments_list)
            print(f"  {'✅ 댓글 작성 완료' if ok else '⚠️  댓글 작성 실패'}: \"{comment_text}\"")

    save_history(history)
    print(f"\n🎉 완료: {success}/{len(batch)}개 업로드")


if __name__ == "__main__":
    main()
