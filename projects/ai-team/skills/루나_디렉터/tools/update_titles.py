"""
update_titles.py — 어제 미국 유튜브 상위 100개 패턴 분석 → 기존 영상 제목 일괄 수정
"""
import os, sys, json, datetime

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, _here)

from src.youtube_uploader import YouTubeUploader
from _shared.ollama_client import chat as lm_chat
from _shared.telegram_notifier import send_telegram_message
from _shared.env_loader import load_env
load_env()

HISTORY_FILE = os.path.join(_root, ".agent", "memory", "upload_history.json")
KNOWLEDGE_FILE = os.path.join(_here, "knowledge", "title_patterns.json")

# ── Step 1: 어제 미국 유튜브 상위 100개 음악 제목 수집 ───────────────────────

def fetch_us_yesterday_titles(youtube) -> list[str]:
    """OAuth 인증으로 미국 유튜브 음악 인기 차트 상위 100개 수집."""
    titles = []
    page_token = None
    for _ in range(2):
        kwargs = dict(part="snippet", chart="mostPopular",
                      regionCode="US", videoCategoryId="10", maxResults=50)
        if page_token:
            kwargs["pageToken"] = page_token
        try:
            res = youtube.videos().list(**kwargs).execute()
            titles += [i["snippet"]["title"] for i in res.get("items", [])]
            page_token = res.get("nextPageToken")
            if not page_token or len(titles) >= 100:
                break
        except Exception as e:
            print(f"  [Warning] 미국 유튜브 수집 실패: {e}")
            break
    print(f"  ✅ 미국 유튜브 제목 {len(titles)}개 수집 완료")
    return titles[:100]


# ── Step 2: Ollama 패턴 분석 → 각 영상 새 제목 생성 ────────────────────────

def generate_new_title(keyword: str, us_titles: list[str]) -> str:
    sample = "\n".join(f"- {t}" for t in us_titles[:50])
    prompt = (
        f"아래는 미국 유튜브에서 조회수가 가장 높은 음악 영상 제목들이야:\n\n"
        f"{sample}\n\n"
        f"이 제목들의 패턴(구조, 길이, 키워드 배치, 특수문자 사용 등)을 분석해서 "
        f"'{keyword}' 테마의 시티팝/K-POP 뮤직비디오 제목을 1개 만들어줘.\n\n"
        "조건:\n"
        "- 수집된 제목 패턴을 그대로 반영해서 자연스럽게 생성\n"
        "- 고정 공식 없음\n"
        "- lofi/lo-fi 금지\n"
        "- 제목 1줄만 출력"
    )
    result = lm_chat(prompt, task="", max_tokens=120)
    if result and result.strip():
        return result.strip().split("\n")[0].strip()
    return keyword


# ── Step 3: YouTube API로 제목 업데이트 ─────────────────────────────────────

def update_video_title(youtube, video_id: str, new_title: str) -> bool:
    try:
        # 기존 snippet 가져오기 (description, tags, categoryId 보존)
        res = youtube.videos().list(part="snippet", id=video_id).execute()
        items = res.get("items", [])
        if not items:
            print(f"  ❌ 영상 없음: {video_id}")
            return False
        snippet = items[0]["snippet"]
        old_title = snippet.get("title", "")
        snippet["title"] = new_title
        youtube.videos().update(
            part="snippet",
            body={"id": video_id, "snippet": snippet}
        ).execute()
        print(f"  ✅ [{video_id}]")
        print(f"     이전: {old_title[:60]}")
        print(f"     변경: {new_title[:60]}")
        return True
    except Exception as e:
        print(f"  ❌ [{video_id}] 업데이트 실패: {e}")
        return False


# ── 메인 ────────────────────────────────────────────────────────────────────

def main():
    print("=" * 60)
    print("  [루나] 제목 일괄 수정 — 미국 유튜브 패턴 기반")
    print("=" * 60)

    # YouTube 인증 먼저
    uploader = YouTubeUploader()
    uploader.authenticate()
    if not uploader.youtube:
        print("❌ YouTube 인증 실패")
        return

    # 실제 채널 영상 목록 수집
    print("  📋 채널 영상 목록 수집 중...")
    ch = uploader.youtube.channels().list(part="contentDetails", mine=True).execute()
    pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
    pl_res = uploader.youtube.playlistItems().list(
        part="snippet", playlistId=pl_id, maxResults=50
    ).execute()

    videos = []
    for item in pl_res.get("items", []):
        vid   = item["snippet"]["resourceId"]["videoId"]
        title = item["snippet"]["title"]
        if "LUNA -" in title.upper():
            keyword = title.upper().split("LUNA -")[-1].split("[")[0].split("(")[0].strip().title()
        else:
            keyword = title.split("|")[-1].strip() or title
        videos.append({"id": vid, "old_title": title, "keyword": keyword})

    print(f"  수정 대상: {len(videos)}개 영상\n")

    # Step 1: 미국 유튜브 인기 차트 수집 (OAuth 사용)
    print("  📡 미국 유튜브 인기 음악 상위 100개 수집 중...")
    us_titles = fetch_us_yesterday_titles(uploader.youtube)

    if not us_titles:
        print("  ⚠️ 수집 실패 — 폴백 패턴으로 진행")

    # 패턴 지식화 저장
    try:
        existing = {}
        if os.path.exists(KNOWLEDGE_FILE):
            with open(KNOWLEDGE_FILE, "r", encoding="utf-8") as f:
                existing = json.load(f)
        today_str = datetime.datetime.utcnow().strftime("%Y-%m-%d")
        existing[today_str] = {"us_top_titles": us_titles[:10], "count": len(us_titles)}
        keys = sorted(existing.keys())[-30:]
        existing = {k: existing[k] for k in keys}
        with open(KNOWLEDGE_FILE, "w", encoding="utf-8") as f:
            json.dump(existing, f, indent=2, ensure_ascii=False)
        print(f"  📚 패턴 지식화 저장 완료 ({KNOWLEDGE_FILE})")
    except Exception as e:
        print(f"  [Warning] 지식화 저장 실패: {e}")

    # YouTube 인증
    uploader = YouTubeUploader()
    uploader.authenticate()
    if not uploader.youtube:
        print("❌ YouTube 인증 실패")
        return

    # Step 2+3: 각 영상 새 제목 생성 → 업데이트
    results = []
    for v in videos:
        print(f"\n  🎯 키워드: {v['keyword']}")
        new_title = generate_new_title(v["keyword"], us_titles)
        ok = update_video_title(uploader.youtube, v["id"], new_title)
        results.append({"id": v["id"], "old": v["old_title"], "new": new_title, "ok": ok})

    # 결과 보고
    ok_count = sum(1 for r in results if r["ok"])
    report = f"✅ <b>[루나]</b> 제목 일괄 수정 완료 ({ok_count}/{len(results)})\n\n"
    for r in results:
        icon = "✅" if r["ok"] else "❌"
        report += f"{icon} {r['new'][:50]}\n"
    send_telegram_message(report)
    print(f"\n{'='*60}")
    print(f"  완료: {ok_count}/{len(results)}개 수정됨")
    print(f"{'='*60}")

    # 업로드 이력 제목 업데이트
    id_to_new = {r["id"]: r["new"] for r in results if r["ok"]}
    if os.path.exists(HISTORY_FILE) and id_to_new:
        with open(HISTORY_FILE, "r", encoding="utf-8") as f:
            history = json.load(f)
        for record in history:
            vid = record.get("metadata", {}).get("video_id", "")
            if vid in id_to_new:
                record["metadata"]["youtube_title"] = id_to_new[vid]
        with open(HISTORY_FILE, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
        print("  📝 upload_history.json 제목 갱신 완료")


if __name__ == "__main__":
    main()
