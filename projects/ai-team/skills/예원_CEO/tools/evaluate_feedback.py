import os
import json
import pickle
from datetime import datetime

# 프로젝트 루트 (.agent/ 가 있는 위치) 자동 탐색
_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", ".."))

MEM_FILE   = os.path.join(PROJECT_ROOT, ".agent", "memory", "upload_history.json")
REWARD_DIR = os.path.join(PROJECT_ROOT, ".agent", "memory", "reward")
PUNISH_DIR = os.path.join(PROJECT_ROOT, ".agent", "memory", "punishment")
TOKEN_FILE = os.path.join(PROJECT_ROOT, ".agent", "credentials", "youtube_token.pickle")

VIEWS_THRESHOLD = 10_000


def _get_youtube():
    """레오 OAuth 토큰으로 YouTube 클라이언트 반환 (없으면 None)."""
    if not os.path.exists(TOKEN_FILE):
        return None
    try:
        from google.auth.transport.requests import Request
        from googleapiclient.discovery import build
        with open(TOKEN_FILE, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
            with open(TOKEN_FILE, "wb") as f:
                pickle.dump(creds, f)
        return build("youtube", "v3", credentials=creds)
    except Exception as e:
        print(f"  [Warning] YouTube 클라이언트 생성 실패: {e}")
        return None


def _fetch_stats(youtube, video_id: str) -> dict:
    """YouTube API로 실제 조회수·좋아요·댓글 수 조회."""
    if not youtube or not video_id or video_id.startswith("DRY-RUN"):
        return {"views": 0, "likes": 0, "comments": 0}
    try:
        resp = youtube.videos().list(part="statistics", id=video_id).execute()
        s = (resp.get("items") or [{}])[0].get("statistics", {})
        return {
            "views":    int(s.get("viewCount",   0)),
            "likes":    int(s.get("likeCount",    0)),
            "comments": int(s.get("commentCount", 0)),
        }
    except Exception as e:
        print(f"  [Warning] 통계 조회 실패 ({video_id}): {e}")
        return {"views": 0, "likes": 0, "comments": 0}


def auto_evaluate_performance():
    if not os.path.exists(MEM_FILE):
        print(f"[Info] 히스토리 파일 없음: {MEM_FILE}")
        return

    os.makedirs(REWARD_DIR, exist_ok=True)
    os.makedirs(PUNISH_DIR, exist_ok=True)

    with open(MEM_FILE, "r", encoding="utf-8") as f:
        history = json.load(f)

    youtube = _get_youtube()
    reward_count = punish_count = 0
    changed = False

    for record in history:
        if record.get("status") != "published":
            continue

        meta     = record.get("metadata", {})
        agent    = record.get("agent", "unknown")
        platform = meta.get("platform", "youtube")

        # 플랫폼별 식별자/제목 분기
        if platform == "instagram":
            title    = meta.get("caption", "캡션 없음")[:50]
            video_id = meta.get("post_id", "")
            stats    = {"views": 0, "likes": 0, "comments": 0}
            score    = 0          # 인스타는 조회수 API 없음 → 별도 평가 보류
        else:
            title    = meta.get("youtube_title", "제목 없음")
            video_id = meta.get("video_id", "")
            stats    = _fetch_stats(youtube, video_id)
            score    = stats["views"]

        summary = {
            "agent":         agent,
            "platform":      platform,
            "title":         title,
            "video_id":      video_id,
            "video_url":     (f"https://youtu.be/{video_id}" if platform != "instagram" and video_id
                              else f"https://www.instagram.com/p/{video_id}/" if video_id else ""),
            "prompt_used":   meta.get("veo_prompt") or meta.get("music_prompt", ""),
            "views":         stats["views"],
            "likes":         stats["likes"],
            "comments":      stats["comments"],
            "feedback_date": datetime.now().strftime("%Y-%m-%d %H:%M"),
        }

        if platform == "instagram":
            summary["conclusion"] = "인스타그램 게시물 — 조회수 API 미지원, 수동 확인 필요."
            log_path = os.path.join(REWARD_DIR, "success_log.jsonl")
            reward_count += 1
            label = "📸 INSTA"
        elif score >= VIEWS_THRESHOLD:
            summary["conclusion"] = "성공. 해당 프롬프트/테마를 다음 기획에 재활용할 것."
            log_path = os.path.join(REWARD_DIR, "success_log.jsonl")
            reward_count += 1
            label = "✅ REWARD"
        else:
            summary["conclusion"] = (
                "조회수 기준 미달. "
                "다음에는 시티팝 멜로디의 템포감 및 비주얼의 복고 감성을 더 보강하여 교정할 것."
            )
            log_path = os.path.join(PUNISH_DIR, "fail_log.jsonl")
            punish_count += 1
            label = "❌ PUNISH"

        with open(log_path, "a", encoding="utf-8") as f:
            f.write(json.dumps(summary, ensure_ascii=False) + "\n")

        print(f"{label} | [{agent}/{platform}] {title[:40]} | {stats['views']:,} views")
        record["status"] = "evaluated"
        changed = True

    if changed:
        with open(MEM_FILE, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
        print(f"\n📊 평가 완료 — REWARD: {reward_count}건 / PUNISH: {punish_count}건")
        print(f"💾 메모리 갱신: {MEM_FILE}")
    else:
        print("ℹ️ 평가할 published 레코드가 없습니다.")


if __name__ == "__main__":
    auto_evaluate_performance()
