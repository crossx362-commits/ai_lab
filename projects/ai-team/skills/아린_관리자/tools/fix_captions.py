"""
fix_captions.py — 기존 인스타 포스팅 캡션 수정 스크립트
구조화 포맷("1. 사진 느낌 설명:") 또는 사진과 맞지 않는 캡션을 Ollama Vision으로 재생성 후 업데이트.
"""
import os
import sys
import json
import re
import requests

_here = os.path.dirname(os.path.abspath(__file__))
# tools → 아린_관리자 → skills → ai-team → projects → ai_lab
_root = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
_ai_team = os.path.abspath(os.path.join(_here, "..", "..", ".."))
for p in (_here, _root, _ai_team):
    if p not in sys.path:
        sys.path.insert(0, p)

from uploader import load_env, ensure_token_fresh
from auto_pipeline import _clean_vision_caption


HISTORY_PATH = os.path.join(_root, "reports", "history", "upload_history.json")
CAPTION_PROMPT = (
    "아래 사진을 보고 인스타그램 캡션을 작성해줘.\n\n"
    "규칙:\n"
    "- 진짜 사람이 폰으로 찍어서 올린 것처럼 자연스럽고 짧게\n"
    "- 1~2문장 + 이모지 1개\n"
    "- 마지막 줄에 해시태그 6~8개\n"
    "- 번호, 제목, 설명 레이블 절대 쓰지 말 것\n"
    "- 캡션 텍스트만 출력, 다른 말 일절 없이\n\n"
    "출력 예시:\n"
    "오늘 이 순간 너무 좋았다 🌊\n"
    "#바다 #여름 #힐링 #감성 #일상 #파도 #여행"
)


def _is_bad_caption(caption: str) -> bool:
    """구조화 포맷 혹은 이상한 캡션 감지."""
    return bool(re.search(r"^\s*\d+\.\s", caption, re.MULTILINE))


def _fetch_image_bytes(url: str) -> bytes | None:
    try:
        res = requests.get(url, timeout=30)
        res.raise_for_status()
        return res.content
    except Exception as e:
        print(f"  ⚠️ 이미지 다운로드 실패 ({url[:60]}): {e}")
        return None


def _regenerate_caption(img_bytes: bytes) -> str | None:
    """Ollama Vision으로 캡션 재생성."""
    from _shared.ollama_client import chat_vision, is_available
    if not is_available():
        print("  ⚠️ Ollama 서버 미사용 가능")
        return None
    result = chat_vision(CAPTION_PROMPT, img_bytes, max_tokens=300)
    if result:
        return _clean_vision_caption(result.strip())
    return None


def _update_instagram_caption(post_id: str, caption: str, access_token: str) -> bool:
    """Instagram Graph API로 캡션 업데이트."""
    res = requests.post(
        f"https://graph.instagram.com/v23.0/{post_id}",
        data={"caption": caption, "access_token": access_token},
        timeout=15,
    ).json()
    if "error" in res:
        print(f"  ❌ 캡션 업데이트 실패: {res['error'].get('message', res['error'])}")
        return False
    return True


def main():
    load_env()
    access_token = ensure_token_fresh()
    if not access_token:
        print("❌ 토큰 없음")
        sys.exit(1)

    history = json.load(open(HISTORY_PATH, encoding="utf-8"))
    arin_posts = [
        h for h in history
        if h.get("agent") == "아린"
        and h.get("metadata", {}).get("platform") == "instagram"
        and h.get("metadata", {}).get("post_id")
        and h.get("metadata", {}).get("image_url")
    ]

    bad_posts = [p for p in arin_posts if _is_bad_caption(p["metadata"].get("caption", ""))]
    print(f"🔍 수정 대상 포스팅: {len(bad_posts)}개 / 전체 {len(arin_posts)}개")

    if not bad_posts:
        print("✅ 수정할 포스팅 없음")
        return

    updated_ids = set()
    for post in bad_posts:
        meta = post["metadata"]
        post_id = meta["post_id"]
        image_url = meta["image_url"]
        print(f"\n📝 수정 중: {post_id} | 트렌드: {meta.get('trend_topic', '?')}")
        print(f"  기존 캡션 앞: {meta['caption'][:80]!r}")

        img_bytes = _fetch_image_bytes(image_url)
        if not img_bytes:
            continue

        new_caption = _regenerate_caption(img_bytes)
        if not new_caption:
            print("  ⚠️ 캡션 재생성 실패 — 건너뜀")
            continue

        print(f"  새 캡션: {new_caption[:120]!r}")

        ok = _update_instagram_caption(post_id, new_caption, access_token)
        if ok:
            meta["caption"] = new_caption
            updated_ids.add(post_id)
            print(f"  ✅ 업데이트 완료!")
        else:
            print(f"  ❌ 업데이트 실패")

    # 히스토리 저장
    if updated_ids:
        with open(HISTORY_PATH, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
        print(f"\n✅ {len(updated_ids)}개 포스팅 캡션 수정 완료, 히스토리 저장됨")


if __name__ == "__main__":
    main()
