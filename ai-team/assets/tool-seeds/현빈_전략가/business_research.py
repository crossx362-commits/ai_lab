#!/usr/bin/env python3
"""
business_research.py — 현빈 (비즈니스 전략가) 리서치 모듈.

흐름:
  1. _load_env()               — .env 로드
  2. _fetch_business_models()  — Gemini 2.5 Flash로 최신 비즈니스 모델 10개 수집
  3. _analyze_insights()       — 수집된 모델에서 AI 팀 적용 가능 인사이트 추출
  4. _save_research()          — hyunbin_research.json 누적 저장 (최대 100개)
  5. _notify_ceo()             — 비서 채널로 텔레그램 보고
  6. run_research()            — 전체 플로우 실행

1시간 주기 cron 또는 직접 실행 모두 지원.
"""
import os
import sys
import json
import datetime

# ── 인코딩 설정 ───────────────────────────────────────────────────────────────
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ── 프로젝트 루트 탐색 ────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)

sys.path.insert(0, _root)
from _shared.telegram_notifier import send_telegram_message
from _shared.env_loader import load_env as _load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# ── 상수 ─────────────────────────────────────────────────────────────────────
_RESEARCH_FILE = os.path.join(_root, ".agent", "memory", "hyunbin_research.json")
_MAX_ITEMS = 100


# ── 내부 유틸리티 ─────────────────────────────────────────────────────────────

def _set_research_goal() -> str:
    """Ollama로 오늘의 비즈니스 리서치 목표 설정. 실패 시 기본값 반환."""
    today = datetime.datetime.now().strftime("%Y-%m-%d (%A)")
    prompt = (
        f"현빈은 AI 비즈니스 전략가야. 오늘({today}) 비즈니스 모델 리서치 목표를 1가지 정해줘.\n"
        "예시: '크리에이터 구독 수익화 모델', 'SaaS 가격 전략', '플랫폼 커뮤니티 비즈니스'\n"
        "딱 한 줄, 20자 이내로만 답해."
    )
    if lm_available():
        result = lm_chat(prompt, max_tokens=60, temperature=0.95)
        if result:
            return result.strip()
    return "AI 크리에이터 수익화 전략"


def _log(msg: str, kind: str = "info") -> None:
    prefix = {
        "info": "📊", "ok": "✅", "warn": "⚠️ ", "err": "❌", "step": "▸",
    }.get(kind, "•")
    print(f"{prefix} {msg}", file=sys.stderr, flush=True)


def _call_ai(prompt: str, max_tokens: int = 3000) -> str:
    """Ollama로 AI 호출. 실패 시 RuntimeError."""
    if lm_available():
        result = lm_chat(prompt, json_mode=True, max_tokens=max_tokens, temperature=0.7)
        if result:
            return result
    raise RuntimeError("Ollama 응답 없음 — ollama 실행 여부 확인")


# ── 핵심 기능 ─────────────────────────────────────────────────────────────────

def _fetch_business_models() -> list[dict]:
    """Gemini로 최신 비즈니스 모델 10개 수집.

    반환 형식:
    [
      {
        "name": "...",
        "category": "...",
        "description": "...",
        "revenue_mechanism": "...",
        "example_companies": "...",
        "why_notable": "..."
      },
      ...
    ]
    """
    _log("비즈니스 모델 수집 중…", "step")
    prompt = (
        "List 6 notable business or monetization models (SaaS, subscription, platform, creator economy, marketplace, freemium, data, community, advertising). "
        "IMPORTANT: You must write all field values (name, category, description, revenue_mechanism, example_companies, why_notable) in Korean (한국어). "
        "Return JSON array only: "
        '[{"name":"","category":"","description":"","revenue_mechanism":"","example_companies":"","why_notable":""}]'
    )
    try:
        raw = _call_ai(prompt, max_tokens=2500)
        models = json.loads(raw.strip())
        if not isinstance(models, list):
            raise ValueError("JSON 배열이 아님")
        _log(f"비즈니스 모델 {len(models)}개 수집 완료", "ok")
        return models
    except Exception as e:
        # 잘린 JSON 복구
        import re
        raw_str = locals().get("raw", "")
        matches = re.findall(r'\{[^{}]+\}', raw_str)
        models = []
        for m in matches:
            try:
                models.append(json.loads(m))
            except Exception:
                pass
        if models:
            _log(f"부분 JSON 복구 성공 ({len(models)}개)", "warn")
            return models
        _log(f"비즈니스 모델 수집 실패: {e}", "err")
        return []


def _analyze_insights(models: list[dict]) -> dict:
    """수집된 비즈니스 모델에서 AI 팀 적용 가능 인사이트 추출.

    반환 형식:
    {
      "key_insights": ["..."],
      "applicable_strategies": ["..."],
      "trending_model": {"name": "...", "summary": "..."}
    }
    """
    _log("인사이트 분석 중…", "step")
    models_json = json.dumps(models, ensure_ascii=False)
    prompt = (
        f"Business models:\n{models_json}\n\n"
        "Extract revenue strategy insights applicable to an AI creator team (Luna: AI music YouTuber, Arin: AI Instagrammer). "
        "IMPORTANT: All generated insights, strategies, and summaries must be written in Korean (한국어). "
        "Return JSON only: "
        '{"key_insights":[""],"applicable_strategies":[""],'
        '"trending_model":{"name":"","summary":""}}'
    )
    try:
        raw = _call_ai(prompt, max_tokens=1500)
        insights = json.loads(raw.strip())
        if not isinstance(insights, dict):
            raise ValueError("JSON 객체가 아님")
        _log("인사이트 분석 완료", "ok")
        return insights
    except Exception as e:
        _log(f"인사이트 분석 실패: {e}", "err")
        return {
            "key_insights": [],
            "applicable_strategies": [],
            "trending_model": {"name": "", "summary": ""},
        }


def _save_research(data: dict) -> None:
    """hyunbin_research.json 에 누적 저장 (name 기준 dedup, 최대 100개 항목)."""
    # 저장 디렉터리 보장
    os.makedirs(os.path.dirname(_RESEARCH_FILE), exist_ok=True)

    # 기존 데이터 로드
    existing: list[dict] = []
    if os.path.exists(_RESEARCH_FILE):
        try:
            with open(_RESEARCH_FILE, "r", encoding="utf-8") as f:
                existing = json.load(f)
            if not isinstance(existing, list):
                existing = []
        except Exception:
            existing = []

    # dedup: name 기준으로 이미 있는 항목 제거 후 새 항목 앞에 추가
    existing_names = {item.get("name", "").lower() for item in existing}
    new_models = data.get("models", [])
    timestamp = data.get("timestamp", "")
    insights = data.get("insights", {})

    added = 0
    for model in new_models:
        name_key = model.get("name", "").lower()
        if name_key and name_key not in existing_names:
            entry = {**model, "_fetched_at": timestamp}
            existing.insert(0, entry)
            existing_names.add(name_key)
            added += 1

    # 최대 100개 유지
    existing = existing[:_MAX_ITEMS]

    # insights 별도 섹션으로 병합 (최신 5개만 보존)
    payload = {
        "models": existing,
        "recent_insights": [],
    }
    # 기존 insights 이력 로드
    if os.path.exists(_RESEARCH_FILE):
        try:
            with open(_RESEARCH_FILE, "r", encoding="utf-8") as f:
                prev = json.load(f)
            payload["recent_insights"] = prev.get("recent_insights", [])
        except Exception:
            pass

    payload["recent_insights"].insert(0, {"timestamp": timestamp, **insights})
    payload["recent_insights"] = payload["recent_insights"][:5]

    with open(_RESEARCH_FILE, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)

    _log(f"research 저장 완료 (신규 {added}개, 누적 {len(existing)}개)", "ok")


def _notify_ceo(insights: dict, timestamp: str) -> bool:
    """비서 채널로 텔레그램 보고. 영숙이 톤으로 가공하여 전송."""
    trending = insights.get("trending_model", {})
    trending_name = trending.get("name", "(정보 없음)")
    trending_summary = trending.get("summary", "")

    key_insights = insights.get("key_insights", [])
    applicable = insights.get("applicable_strategies", [])

    # 핵심 인사이트 포맷
    insights_text = ""
    for i, item in enumerate(key_insights[:5], 1):
        insights_text += f"  {i}. {item}\n"
    if not insights_text:
        insights_text = "  (없음)\n"

    # AI 팀 적용 전략 포맷
    strategies_text = ""
    for i, item in enumerate(applicable[:5], 1):
        strategies_text += f"  {i}. {item}\n"
    if not strategies_text:
        strategies_text = "  (없음)\n"

    raw_report = (
        f"📊 [현빈 → 비서] 비즈니스 모델 리서치 보고 ({timestamp})\n"
        f"🎯 오늘의 목표: {insights.get('_goal', '')}\n\n"
        f"🔥 주목 모델: <b>{trending_name}</b>\n"
        f"{trending_summary}\n\n"
        f"💡 핵심 인사이트:\n{insights_text}\n"
        f"🚀 AI 팀 적용 전략:\n{strategies_text}\n"
        "비서님, CEO님과 논의 부탁드립니다. — 현빈"
    )

    # 영숙 페르소나 정의
    yeongsuk_persona = (
        "당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 동료입니다. "
        "말투는 자연스럽고 친근하며 이모지를 적절히 사용합니다. "
        "현빈이가 작성해 준 비즈니스 모델 리서치 보고서를 바탕으로, "
        "CEO님께 애교 있고 싹싹하게 한글로 브리핑하는 텔레그램 메시지를 예쁘게 작성해주세요. "
        "정보 가독성을 위해 HTML 태그는 절대 사용하지 마시고, 줄바꿈과 이모지만을 활용하여 정돈해 주세요. "
        "메시지 마지막에 영숙이의 코멘트나 응원을 담아주세요."
    )

    prompt = f"{yeongsuk_persona}\n\n다음 현빈이의 원본 보고서 내용을 바탕으로 CEO님께 전송할 친근하고 정돈된 보고 메시지를 작성해줘. 절대로 HTML 태그를 포함하지 마세요:\n\n{raw_report}"

    try:
        msg = _call_ai(prompt, max_tokens=1500)
    except Exception as e:
        _log(f"영숙이 톤 변환 실패 ({e}) — 원본 보고서로 발송", "warn")
        msg = raw_report

    ok = send_telegram_message(msg, parse_mode="")
    _log(f"텔레그램 보고 {'완료' if ok else '실패'}", "ok" if ok else "warn")
    return ok


# ── 공개 진입점 ───────────────────────────────────────────────────────────────

def run_research() -> bool:
    """전체 비즈니스 리서치 플로우 실행. 성공 시 True 반환."""
    _load_env()

    # KST 타임스탬프
    kst = datetime.timezone(datetime.timedelta(hours=9))
    now_kst = datetime.datetime.now(kst)
    timestamp = now_kst.strftime("%Y-%m-%d %H:%M KST")

    _log(f"현빈 비즈니스 리서치 시작 ({timestamp})", "info")

    # 0단계: Ollama로 오늘의 리서치 목표 설정
    goal = _set_research_goal()
    _log(f"오늘의 목표: {goal}", "step")

    # 1. 비즈니스 모델 수집
    models = _fetch_business_models()
    if not models:
        _log("비즈니스 모델 수집 실패 — Ollama 미응답", "err")
        send_telegram_message(f"⚠️ [현빈] {timestamp} 리서치 실패 — Ollama 응답 없음. 올라마 실행 상태를 확인해주세요.")
        return False

    # 2. 인사이트 분석
    insights = _analyze_insights(models)

    # 3. 파일 저장
    _save_research({
        "timestamp": timestamp,
        "models": models,
        "insights": insights,
    })

    # 4. 비서 채널로 보고 (목표 포함)
    insights["_goal"] = goal
    ok = _notify_ceo(insights, timestamp)

    _log(f"리서치 사이클 완료 ({'성공' if ok else '텔레그램 실패'})", "ok" if ok else "warn")
    return ok


# ── 직접 실행 ─────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    success = run_research()
    sys.exit(0 if success else 1)
