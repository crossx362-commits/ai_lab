"""
fix_issues.py — 가희 수정 요청 타깃 픽스
전체 파이프라인 없이 문제 video_id / post_id만 핀포인트 수정.

실행:
  python fix_issues.py   # 가희 최신 검수 결과 기반 자동 수정
"""
import os, sys, json, datetime

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.telegram_notifier import send_telegram_message

load_env()
KST = datetime.timezone(datetime.timedelta(hours=9))

# ─── 가희 검수 결과 로드 (inspection_log.jsonl 우선, 없으면 하드코딩 fallback) ──
_INSPECT_LOG = os.path.join(_root, "reports", "learning", "gahee_inspection_log.jsonl")

def _load_issues_from_log():
    """최신 미해결 검수 이슈를 gahee_inspection_log.jsonl에서 로드."""
    yt, insta = [], []
    if not os.path.exists(_INSPECT_LOG):
        return yt, insta
    try:
        with open(_INSPECT_LOG, encoding="utf-8") as f:
            for line in f:
                rec = json.loads(line.strip())
                if rec.get("resolved"):
                    continue
                if rec.get("platform") == "youtube":
                    yt.append({"id": rec["content_id"], "title": rec.get("title",""),
                                "issue": "; ".join(rec.get("violations", []) + rec.get("warnings", [])),
                                "fix": rec.get("fix_type", "add_music_keyword_and_ai_disclosure")})
                elif rec.get("platform") == "instagram":
                    insta.append({"id": rec["content_id"], "caption": rec.get("caption",""),
                                  "issue": "; ".join(rec.get("violations", []) + rec.get("warnings", [])),
                                  "fix": "regenerate_caption"})
    except Exception as e:
        print(f"  [검수 로그 로드 오류] {e}")
    return yt, insta

_log_yt, _log_insta = _load_issues_from_log()

# 로그가 있으면 로그 사용, 없으면 가장 최근 가희 판정 결과(하드코딩) 사용
YT_ISSUES = _log_yt if _log_yt else [
    # hk205UDU3s0: 쇼츠 형식(9:16) 파이프라인 위반 + #Shorts 제목 문제
    {
        "id":    "hk205UDU3s0",
        "title": "LUNA - Neon City Lights #Shorts (밤하늘의 속삭임)",
        "issue": "쇼츠 형식(9:16) 파이프라인 위반 + #Shorts 제목 + LUNA 접두어",
        "fix":   "make_private_shorts_violation",
    },
    # LUNA - 접두어 규칙 위반 (채널명 중복, 파이프라인 규칙: LUNA 붙이지 않음)
    {
        "id":    "5l_UfNu-DPA",
        "title": "LUNA - Neon Bloom Official Music Video (밤의 서울, 빛나는 순간)",
        "issue": "제목 앞 'LUNA -' 접두어 + Official Music Video 대괄호 누락",
        "fix":   "fix_luna_title_prefix",
    },
    {
        "id":    "rQmcfl2n7a8",
        "title": "LUNA - Stardust Bloom [Official Music Video] (별빛 아래 흩날리는 기억)",
        "issue": "제목 앞 'LUNA -' 접두어",
        "fix":   "fix_luna_title_prefix",
    },
    {
        "id":    "iHHUMfFG-rk",
        "title": "LUNA - Starlight Serenade Official Music Video (황금빛 해안의 밤)",
        "issue": "제목 앞 'LUNA -' 접두어 + Official Music Video 대괄호 누락",
        "fix":   "fix_luna_title_prefix",
    },
]

INSTA_ISSUES = _log_insta if _log_insta else [
    # 아린 Instagram: 금지 키워드(인공지능/AI 계열) 포함 포스팅
    # post_id는 Instagram API 조회 필요 — 아린 auto_pipeline 검수 결과 기반
    {
        "id":      "gahee_insta_1",
        "caption": "🔥 오늘의 핫 트렌드: 인공지능 이미지 생성 기술! 🚀\n안녕하세요! 아...",
        "issue":   "금지 키워드: 인공지능, ai, ai 생성, 오늘의 ai, 체험해보세요",
        "fix":     "regenerate_caption",
    },
]


# ─── YouTube 수정 함수 ───────────────────────────────────────────────────────

def _get_youtube():
    import pickle
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build
    token_file = os.path.join(_root, "projects", "ai-team", "skills", "루나_디렉터", "tools", "youtube_token.pickle")
    if not os.path.exists(token_file):
        return None
    try:
        with open(token_file, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
        return build("youtube", "v3", credentials=creds)
    except Exception as e:
        print(f"  [YouTube 인증] {e}")
        return None


def _get_real_video_ids(youtube) -> list[dict]:
    """채널에서 REJECT/REVIEW 영상 실제 ID 조회."""
    try:
        ch = youtube.channels().list(part="contentDetails", mine=True).execute()
        pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
        res = youtube.playlistItems().list(
            part="snippet", playlistId=pl_id, maxResults=50
        ).execute()
        return [
            {"id": item["snippet"]["resourceId"]["videoId"],
             "title": item["snippet"]["title"]}
            for item in res.get("items", [])
        ]
    except Exception as e:
        print(f"  [채널 조회] {e}")
        return []


def make_private_shorts_violation(youtube, video_id: str) -> bool:
    """쇼츠 형식 파이프라인 위반 영상 → 비공개 처리 + 텔레그램 알림."""
    try:
        youtube.videos().update(
            part="status",
            body={"id": video_id, "status": {"privacyStatus": "private"}},
        ).execute()
        print(f"  🔒 쇼츠 위반 비공개 처리: {video_id}")
        send_telegram_message(
            f"⚠️ [가희] {video_id} 쇼츠 형식(9:16) 파이프라인 위반 → 비공개 처리됨\n"
            f"루나에게 16:9 재제작 요청 필요."
        )
        return True
    except Exception as e:
        print(f"  ❌ 비공개 처리 실패 ({video_id}): {e}")
        return False


def fix_luna_title_prefix(youtube, video_id: str) -> bool:
    """'LUNA - ' 접두어 제거 + [Official Music Video] 대괄호 통일."""
    import re
    try:
        res = youtube.videos().list(part="snippet", id=video_id).execute()
        items = res.get("items", [])
        if not items:
            print(f"  ❌ 영상 없음: {video_id}")
            return False
        snippet = items[0]["snippet"]
        old = snippet.get("title", "")

        # 규칙: "LUNA - " 접두어 제거
        new_title = re.sub(r"^LUNA\s*[-–]\s*", "", old).strip()
        # 규칙: "Official Music Video" → "[Official Music Video]" (대괄호 없으면 추가)
        new_title = re.sub(
            r"(?<!\[)Official Music Video(?!\])",
            "[Official Music Video]",
            new_title
        )

        if new_title == old:
            print(f"  ✅ [{video_id}] 이미 올바른 제목, 건너뜀")
            return True

        snippet["title"] = new_title
        youtube.videos().update(part="snippet", body={"id": video_id, "snippet": snippet}).execute()
        print(f"  ✅ [{video_id}] 제목 수정")
        print(f"     이전: {old[:70]}")
        print(f"     변경: {new_title[:70]}")
        return True
    except Exception as e:
        print(f"  ❌ [{video_id}] 수정 실패: {e}")
        return False


def fix_youtube_title(youtube, video_id: str, old_title: str, fix_type: str) -> bool:
    """fix_type 라우터 — 가희 판정에 따라 적절한 수정 함수 호출."""
    if fix_type == "make_private_shorts_violation":
        return make_private_shorts_violation(youtube, video_id)
    if fix_type == "fix_luna_title_prefix":
        return fix_luna_title_prefix(youtube, video_id)

    # 기본: 음악 키워드 추가 + AI 공시
    try:
        res = youtube.videos().list(part="snippet", id=video_id).execute()
        items = res.get("items", [])
        if not items:
            print(f"  ❌ 영상 없음: {video_id}")
            return False
        snippet = items[0]["snippet"]
        old = snippet.get("title", "")

        if lm_available():
            prompt = (
                f"다음 유튜브 음악 영상 제목에 음악 관련 키워드(BGM, Music, MV 중 하나)를 "
                f"자연스럽게 추가해줘. 제목 1줄만 출력.\n원본: {old}"
            )
            new_title = lm_chat(prompt, max_tokens=80, temperature=0.3)
            new_title = new_title.strip().split("\n")[0] if new_title else old
        else:
            new_title = old.rstrip() + " | BGM"

        snippet["title"] = new_title

        if fix_type == "add_music_keyword_and_ai_disclosure":
            desc = snippet.get("description", "")
            ai_notice = "※ This music is AI-generated. / 이 음악은 AI로 생성되었습니다."
            if ai_notice not in desc:
                snippet["description"] = ai_notice + "\n\n" + desc

        youtube.videos().update(part="snippet", body={"id": video_id, "snippet": snippet}).execute()
        print(f"  ✅ [{video_id}] 수정 완료")
        print(f"     이전: {old[:55]}")
        print(f"     변경: {new_title[:55]}")
        return True

    except Exception as e:
        print(f"  ❌ [{video_id}] 수정 실패: {e}")
        return False


def restore_youtube_public(youtube, video_id: str) -> bool:
    """수정 완료 후 비공개 → 공개 전환."""
    try:
        youtube.videos().update(
            part="status",
            body={"id": video_id, "status": {"privacyStatus": "public"}},
        ).execute()
        print(f"  🔓 공개 복원: {video_id}")
        return True
    except Exception as e:
        print(f"  ❌ 공개 복원 실패 ({video_id}): {e}")
        return False


# ─── Instagram 수정 함수 ────────────────────────────────────────────────────

_BANNED = [
    "미래", "인공지능", "ai", "기계", "테크", "로봇", "첨단기술",
    "4차산업", "딥러닝", "머신러닝", "ai 생성", "인공지능이 만든",
    "오늘의 ai", "체험해보세요", "경험해보세요", "lofi", "lo-fi",
]


def regenerate_caption(old_caption: str) -> str:
    """금지 키워드 없이 Ollama로 캡션 재생성."""
    if lm_available():
        prompt = (
            f"이 인스타 캡션 다시 써줘. "
            f"진짜 사람이 쓴 것처럼 짧고 자연스럽게, 친구한테 말하듯. "
            f"AI·인공지능·미래·테크 단어 빼고. 이모지 1개, 해시태그 유지. 캡션만 출력.\n\n"
            f"원본:\n{old_caption[:300]}"
        )
        result = lm_chat(prompt, max_tokens=200, temperature=0.7)
        if result and result.strip():
            return result.strip()
    # Ollama 없으면 금지 키워드만 제거
    lines = [l for l in old_caption.split("\n")
             if not any(kw in l.lower() for kw in _BANNED)]
    return "\n".join(lines).strip() or "오늘도 좋은 하루 🌿 #일상 #감성"


def fix_instagram_post(post_id: str, old_caption: str) -> bool:
    """Instagram Graph API로 캡션 수정."""
    import urllib.request, urllib.parse
    token      = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    if not token:
        print(f"  ⚠️ Instagram 토큰 없음 — 캡션 재생성만 출력")
        new_cap = regenerate_caption(old_caption)
        print(f"  📝 재생성 캡션:\n{new_cap[:200]}")
        return False
    try:
        new_caption = regenerate_caption(old_caption)
        data = urllib.parse.urlencode({"caption": new_caption, "access_token": token}).encode()
        req  = urllib.request.Request(
            f"https://graph.instagram.com/v23.0/{post_id}",
            data=data, method="POST"
        )
        with urllib.request.urlopen(req, timeout=15) as r:
            result = json.loads(r.read())
        if result.get("success") or result.get("id"):
            print(f"  ✅ Instagram [{post_id}] 캡션 수정 완료")
            print(f"  📝 새 캡션: {new_caption[:100]}")
            return True
        print(f"  ⚠️ Instagram 수정 응답: {result}")
        return False
    except Exception as e:
        print(f"  ❌ Instagram [{post_id}] 수정 실패: {e}")
        return False


# ─── 메인 ───────────────────────────────────────────────────────────────────

def main():
    kst_now = datetime.datetime.now(KST).strftime("%Y-%m-%d %H:%M KST")
    print(f"\n{'='*55}")
    print(f"🔧 [가희 → 루나/아린] 타깃 픽스 시작 ({kst_now})")
    print(f"{'='*55}\n")

    results = []

    # ── YouTube 수정 ──────────────────────────────────────────────────────────
    print("── YouTube 핀포인트 수정 중...")
    youtube = _get_youtube()
    if youtube:
        for issue in YT_ISSUES:
            vid = issue["id"]
            print(f"\n  🎯 [{vid}] {issue['title'][:55]}")
            print(f"     이슈: {issue['issue']}")
            ok = fix_youtube_title(youtube, vid, issue["title"], issue["fix"])
            # 쇼츠 위반은 비공개 유지; 그 외 수정 완료 시 공개 복원
            if ok and issue["fix"] != "make_private_shorts_violation":
                restore_youtube_public(youtube, vid)
            results.append({"platform": "YouTube", "id": vid, "ok": ok,
                             "fix": issue["fix"]})
    else:
        print("  ⚠️ YouTube 인증 없음 — YouTube 수정 건너뜀")

    # ── Instagram 수정 ────────────────────────────────────────────────────────
    print("\n── Instagram 핀포인트 수정 중...")
    for issue in INSTA_ISSUES:
        print(f"\n  🎯 [{issue['id']}]")
        print(f"     이슈: {issue['issue']}")
        ok = fix_instagram_post(issue["id"], issue["caption"])
        results.append({"platform": "Instagram", "id": issue["id"], "ok": ok})

    # ── 결과 보고 ─────────────────────────────────────────────────────────────
    ok_count = sum(1 for r in results if r["ok"])
    print(f"\n{'─'*40}")
    print(f"✅ 수정 완료: {ok_count}/{len(results)}건")

    lines = [
        f"🔧 [가희 타깃 픽스] 완료 ({kst_now})",
        f"총 {len(results)}건 | 성공 {ok_count}건 | 실패 {len(results)-ok_count}건\n",
    ]
    for r in results:
        icon = "✅" if r["ok"] else "❌"
        lines.append(f"{icon} [{r['platform']}] {r['id']}")
    lines.append("\n가희에게 재검수 요청 필요.")
    send_telegram_message("\n".join(lines))

    print(f"{'='*55}\n")


if __name__ == "__main__":
    main()
