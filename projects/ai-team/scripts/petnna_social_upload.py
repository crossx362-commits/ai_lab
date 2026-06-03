#!/usr/bin/env python3
"""
petnna_social_upload.py
모든 에이전트 작업물 → 펫과나 소셜 피드 자동 업로드 (Ollama 캡션 생성)

Usage: cd /Users/junholee/ai_lab && python3 projects/ai-team/scripts/petnna_social_upload.py
"""
import json, os, sys, time, urllib.request
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

SB_URL   = os.getenv("SUPABASE_URL", "")
SB_KEY   = os.getenv("SUPABASE_ANON_KEY", "")
SB_SKEY  = os.getenv("SUPABASE_SERVICE_ROLE_KEY", "")
HISTORY  = ROOT / "reports/history/petnna_social_upload_history.json"

# ── 에이전트 정의 ────────────────────────────────────────────────────────────
AGENTS = {
    "루나": {
        "emoji": "🎵",
        "theme": "AI 시티팝 뮤직비디오",
        "pet_angle": "반려동물과 함께 듣는 힐링 음악",
        "upload_dir": UPLOADS / "luna",
        "file_glob": "best_scene_thumbnail*.png",
    },
    "아린": {
        "emoji": "📸",
        "theme": "인스타그램 감성 비주얼",
        "pet_angle": "반려동물 일상 포토 감성",
        "upload_dir": UPLOADS / "arin",
        "file_glob": "*.png",
    },
    "레오": {
        "emoji": "🎬",
        "theme": "반려동물 바이럴 마케팅 팁",
        "pet_angle": "우리 펫을 SNS 스타로 만드는 방법",
        "upload_dir": None,
        "file_glob": None,
    },
    "현빈": {
        "emoji": "💡",
        "theme": "펫 케어 비즈니스 인사이트",
        "pet_angle": "스마트한 집사를 위한 펫 케어 꿀팁",
        "upload_dir": None,
        "file_glob": None,
    },
    "티모": {
        "emoji": "🎨",
        "theme": "반려동물 생활 디자인",
        "pet_angle": "펫과 집사가 함께하는 인테리어 & 라이프스타일",
        "upload_dir": None,
        "file_glob": None,
    },
    "가희": {
        "emoji": "🏥",
        "theme": "반려동물 건강 케어 체크리스트",
        "pet_angle": "우리 아이 건강을 지키는 일상 습관",
        "upload_dir": None,
        "file_glob": None,
    },
    "경수": {
        "emoji": "🛡️",
        "theme": "반려동물 안전 가이드",
        "pet_angle": "집사가 꼭 알아야 할 펫 안전 수칙",
        "upload_dir": None,
        "file_glob": None,
    },
    "영숙": {
        "emoji": "📅",
        "theme": "반려동물 케어 스케줄러",
        "pet_angle": "이번 주 우리 아이 케어 일정 체크!",
        "upload_dir": None,
        "file_glob": None,
    },
}


def load_history() -> dict:
    try:
        return json.loads(HISTORY.read_text())
    except Exception:
        return {"uploaded": [], "last_agent_idx": 0}


def save_history(h: dict):
    HISTORY.parent.mkdir(parents=True, exist_ok=True)
    HISTORY.write_text(json.dumps(h, ensure_ascii=False, indent=2))


def supabase_insert(row: dict) -> bool:
    if not SB_URL:
        print("  ⚠️  Supabase URL 없음")
        return False
    key = SB_SKEY or SB_KEY
    req = urllib.request.Request(
        f"{SB_URL}/rest/v1/posts",
        data=json.dumps(row).encode(),
        headers={
            "apikey": key,
            "Authorization": f"Bearer {key}",
            "Content-Type": "application/json",
            "Prefer": "return=minimal",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status in (200, 201)
    except Exception as e:
        print(f"  ❌ Supabase 오류: {e}")
        return False


def generate_caption(agent_name: str, pet_angle: str, extra: str = "") -> str:
    today = datetime.now().strftime("%m월 %d일")
    prompt = f"""펫과나 반려동물 케어 앱 소셜 피드 게시물 캡션을 작성하세요.

에이전트: {agent_name}
주제: {pet_angle}
{f'추가 컨텍스트: {extra}' if extra else ''}
날짜: {today}

요구사항:
- 반려동물 케어 관련 실용적이고 따뜻한 내용
- 2-3줄, 이모지 2-3개
- 해시태그 3개 (예: #펫과나 #반려동물 #반려견케어)
- 팔로워가 저장하고 싶은 유용한 팁 포함

캡션만 출력하세요. 설명 없이."""
    result = lm.chat(prompt, task="", temperature=0.75)
    return (result or f"{pet_angle} 🐾 #펫과나 #반려동물").strip()


def get_image_for_agent(agent_cfg: dict, history_uploaded: set) -> tuple[str | None, str]:
    """이미지 파일 찾아 업로드 → (url, history_key)"""
    if not agent_cfg.get("upload_dir"):
        return None, ""
    d = agent_cfg["upload_dir"]
    if not d.exists():
        return None, ""
    files = sorted(d.glob(agent_cfg["file_glob"]), key=lambda p: p.stat().st_mtime, reverse=True)
    for f in files:
        key = str(f)
        if key in history_uploaded:
            continue
        print(f"  📤 이미지 업로드: {f.name}")
        img_bytes = f.read_bytes()
        url = upload_image(img_bytes, f.name)
        if url:
            print(f"  🔗 {url}")
            return url, key
    return None, ""


def post_for_agent(agent_name: str, history: dict) -> bool:
    uploaded_set = set(history.get("uploaded", []))
    cfg = AGENTS[agent_name]

    # 오늘 이미 올렸으면 건너뜀
    today_key = f"{agent_name}_{datetime.now().strftime('%Y-%m-%d')}"
    if today_key in uploaded_set:
        print(f"  ⏭️  오늘 이미 업로드됨")
        return False

    # 이미지
    img_url, img_key = get_image_for_agent(cfg, uploaded_set)

    # 캡션
    print(f"  🤖 캡션 생성 중...")
    caption = generate_caption(agent_name, cfg["pet_angle"], cfg["theme"])
    print(f"  📝 {caption[:80]}...")

    # Supabase INSERT
    row = {
        "user_id": "00000000-0000-0000-0000-000000000001",
        "pet_name": f"{cfg['emoji']} {agent_name}",
        "pet_avatar": cfg["emoji"],
        "content": caption,
        "image": img_url,
        "is_video": False,
        "likes": 0,
        "comments": json.dumps([]),
    }

    ok = supabase_insert(row)
    status = "✅ 업로드 완료" if ok else "⚠️  Supabase 실패"
    print(f"  {status}")

    if ok or img_url:
        history["uploaded"].append(today_key)
        if img_key:
            history["uploaded"].append(img_key)

    return ok


def main():
    if not lm.is_available():
        print("❌ Ollama 서버 미실행 (http://localhost:11434)")
        sys.exit(1)

    model = lm._pick_model(lm._list_models(), "") or "auto"
    print(f"🚀 펫과나 소셜 업로드 — {datetime.now().strftime('%Y-%m-%d %H:%M')} | Ollama/{model}")

    history = load_history()
    agent_names = list(AGENTS.keys())

    # 이번 실행에서 처리할 에이전트 (로테이션 — 한 번에 2-3개씩)
    idx = history.get("last_agent_idx", 0) % len(agent_names)
    batch = agent_names[idx:idx+3]
    history["last_agent_idx"] = (idx + 3) % len(agent_names)

    print(f"📋 이번 배치: {', '.join(batch)}\n")

    success = 0
    for name in batch:
        print(f"\n{'='*50}\n[{AGENTS[name]['emoji']} {name}] {AGENTS[name]['pet_angle']}")
        ok = post_for_agent(name, history)
        if ok:
            success += 1
        time.sleep(3)

    save_history(history)
    print(f"\n✅ 완료: {success}/{len(batch)}개 | 다음 배치 인덱스: {history['last_agent_idx']}")


if __name__ == "__main__":
    main()
