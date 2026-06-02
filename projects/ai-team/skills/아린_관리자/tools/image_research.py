"""
image_research.py — 아린 자가학습 모듈
1시간마다 웹·YouTube에서 좋은 이미지 트렌드를 학습 → 프롬프트 지식 파일 업데이트.
학습 결과는 prompt_crafter.py와 auto_pipeline.py에 자동 반영.
"""
import os
import sys
import json
import random
import datetime


_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent", "tools")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

RESEARCH_FILE = os.path.join(_root, ".agent", "memory", "arin_research.json")
MAX_PROMPTS   = 60


from _shared.env_loader import load_env as _load_env


def _load_research() -> dict:
    if os.path.exists(RESEARCH_FILE):
        try:
            with open(RESEARCH_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {"learned_prompts": [], "style_trends": [], "research_count": 0}


def _save_research(data: dict):
    os.makedirs(os.path.dirname(RESEARCH_FILE), exist_ok=True)
    data["last_updated"] = datetime.datetime.now().isoformat()
    with open(RESEARCH_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


# ─── 1단계: 트렌딩 이미지 테마 수집 (Gemini 지식 기반) ─────────────────────

_COLLECT_PROMPT = (
    "List 10 trending image styles on Instagram and Pinterest. "
    "Focus ONLY on: spring/summer/autumn/winter seasons, mountains, ocean/beach, forests, animals, people/lifestyle, food/cafe, travel. "
    "Absolutely NO tech, AI, or future themes. "
    "Make each theme visually distinct — vary seasons, locations, and subjects for maximum diversity. "
    "Return JSON array only: "
    '[{"theme":"","category":"spring|summer|autumn|winter|mountain|landscape|animal|person|food|travel","visual_style":"","color_palette":"","mood":"","popular_on":"instagram|pinterest"}]'
)


def _set_research_goal() -> str:
    """Ollama로 오늘의 이미지 리서치 목표 설정. 실패 시 기본값 반환."""
    today = datetime.datetime.now().strftime("%Y-%m-%d (%A)")
    prompt = (
        f"아린은 AI 인스타그램 매니저야. 오늘({today}) 이미지 트렌드 리서치 목표를 1가지 정해줘.\n"
        "예시: '봄 꽃 감성 피드', '제주도 여행 무드', '카페 브런치 스타일'\n"
        "딱 한 줄, 20자 이내로만 답해."
    )
    if lm_available():
        result = lm_chat(prompt, max_tokens=60, temperature=0.95)
        if result:
            return result.strip()
    return "자연/여행 감성 이미지 트렌드"


def _call_ai(prompt: str, max_tokens: int = 1500) -> str | None:
    """Ollama로 AI 호출. 결과 텍스트 반환."""
    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=max_tokens, temperature=0.7)
        if raw:
            return raw
    print("  [아린 리서치] Ollama 응답 없음 — 건너뜀")
    return None


def _fetch_image_trends() -> list[dict]:
    raw = _call_ai(_COLLECT_PROMPT, max_tokens=2000)
    if not raw:
        print("  [아린 리서치] 트렌드 수집 실패")
        return []
    try:
        items = json.loads(raw.strip())
        return items if isinstance(items, list) else []
    except Exception as e:
        # 잘린 JSON 복구: 완전한 오브젝트만 추출
        import re
        matches = re.findall(r'\{[^{}]+\}', raw)
        items = []
        for m in matches:
            try:
                items.append(json.loads(m))
            except Exception:
                pass
        if items:
            print(f"  [아린 리서치] 부분 JSON 복구 성공 ({len(items)}개)")
            return items
        print(f"  [아린 리서치] 트렌드 파싱 실패: {e}")
        return []


# ─── 2단계: 이미지 생성 프롬프트 추출 ────────────────────────────────────────

_PROMPT_GEN = (
    "Based on these Instagram image trends, create 5 diverse photorealistic image generation prompts.\n"
    "Trends:\n{trends}\n"
    "Rules:\n"
    "- Each prompt must use a DIFFERENT season/location/subject — no two prompts should look similar\n"
    "- Vary photographic style: e.g., aerial drone, macro, long exposure, golden hour, misty morning, underwater\n"
    "- Natural/seasonal/travel/animal/food/person themes ONLY — absolutely no tech or AI themes\n"
    "- Each image_prompt must be detailed, specific, and in English\n"
    "Return JSON array only (exactly 5 items):\n"
    '[{{"category":"spring|summer|autumn|winter|mountain|landscape|animal|person|food|travel","theme":"","image_prompt":"","hashtags":["","","","",""],"style_note":""}}]'
)


def _extract_prompts(trends: list[dict]) -> list[dict]:
    if not trends:
        return []
    trend_text = "\n".join(
        f"- {t.get('theme','')} ({t.get('category','')}) | {t.get('visual_style','')} | {t.get('mood','')}"
        for t in trends[:12]
    )
    prompt = _PROMPT_GEN.format(trends=trend_text)
    raw = _call_ai(prompt, max_tokens=2000)
    if not raw:
        print("  [아린 리서치] 프롬프트 추출 실패")
        return []
    try:
        items = json.loads(raw.strip())
        now = datetime.datetime.now().isoformat()
        for it in (items if isinstance(items, list) else []):
            it["created_at"] = now
        return items if isinstance(items, list) else []
    except Exception as e:
        print(f"  [아린 리서치] 프롬프트 파싱 실패: {e}")
        return []


# ─── 3단계: 지식 파일 저장 + 비서 보고 ──────────────────────────────────────

def _merge_and_save(existing: dict, new_prompts: list[dict], new_trends: list[dict]) -> dict:
    all_prompts = existing.get("learned_prompts", []) + new_prompts
    # 테마 중복 제거 (최신 우선)
    seen, deduped = set(), []
    for p in reversed(all_prompts):
        key = p.get("theme", p.get("image_prompt", ""))[:30].lower()
        if key not in seen:
            seen.add(key)
            deduped.append(p)
    deduped.reverse()
    existing["learned_prompts"] = deduped[-MAX_PROMPTS:]

    # 스타일 트렌드 누적
    new_styles = [f"{t.get('theme','')} — {t.get('visual_style','')}" for t in new_trends[:5]]
    existing["style_trends"] = list(dict.fromkeys(
        existing.get("style_trends", []) + new_styles
    ))[-20:]

    existing["research_count"] = existing.get("research_count", 0) + 1
    _save_research(existing)
    return existing


def run_research() -> bool:
    """1회 아린 이미지 리서치 사이클. 성공 시 True 반환."""
    _load_env()
    api_key = os.getenv("GEMINI_API_KEY", "")
    if not api_key:
        print("  [아린 리서치] GEMINI_API_KEY 없음")
        return False

    # 0단계: Ollama로 오늘의 리서치 목표 설정
    goal = _set_research_goal()
    print(f"  [아린 리서치] 오늘의 목표: {goal}")

    print("  [아린 리서치] 이미지 트렌드 수집 중...")
    trends = _fetch_image_trends()
    if not trends:
        send_telegram_message("⚠️ [아린] 이미지 리서치 실패 — Ollama 응답 없음. 올라마 실행 상태 확인 필요.")
        return False

    print(f"  [아린 리서치] {len(trends)}개 트렌드 → 프롬프트 추출 중...")
    new_prompts = _extract_prompts(trends)

    existing = _load_research()
    merged   = _merge_and_save(existing, new_prompts, trends)

    total = len(merged["learned_prompts"])
    count = merged["research_count"]
    print(f"  [아린 리서치] 완료 — 누적 프롬프트 {total}개, 총 {count}회")

    # 텔레그램 보고서 발송
    themes_str = ", ".join(p.get("theme", "") for p in new_prompts if p.get("theme"))
    msg = (
        f"🌸 [아린 → 비서] 이미지 트렌드 자가 학습 완료!\n\n"
        f"🎯 오늘의 리서치 목표: {goal}\n"
        f"💡 새 학습 프롬프트 테마: {themes_str}\n"
        f"📊 총 리서치 횟수: {count}회 | 누적 학습 프롬프트: {total}개\n"
        f"학습된 프롬프트가 다음 인스타그램 포스팅에 반영됩니다!"
    )
    store_knowledge('아린', 'Instagram Trend Research', report, ['Trend', 'Instagram'])\n    send_telegram_message(msg)

    return True


def get_random_learned_prompt() -> dict | None:
    """auto_pipeline.py에서 호출 — 학습된 프롬프트 중 랜덤 반환."""
    data = _load_research()
    prompts = data.get("learned_prompts", [])
    return random.choice(prompts) if prompts else None


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    run_research()
