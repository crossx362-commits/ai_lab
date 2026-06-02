"""
content_inspector.py — 가희: YouTube·Instagram 콘텐츠 품질·정책 위반 검수

검수 모드:
  NEW_UPLOAD       — 업로드 전 사전 심사 (REJECT 시 즉시 반려)
  EXISTING_CONTENT — 채널 전체 사후 스캔
  POST_UPLOAD      — 업로드 직후 실제 게시물 검수 + 수정 요청

정기 검수 (하루 3회): morning(07:00) / afternoon(13:00) / night(21:00) KST

실행:
  python content_inspector.py                          # 채널 전체 스캔
  python content_inspector.py --id <VIDEO_ID>          # YouTube 단건 검수
  python content_inspector.py --new                    # 오늘 신규 업로드 사전 검수
  python content_inspector.py --schedule morning       # 오전 정기 검수 (YouTube+Instagram)
  python content_inspector.py --schedule afternoon     # 오후 정기 검수
  python content_inspector.py --schedule night         # 야간 정기 검수
  python content_inspector.py --pre-upload <CAPTION>   # 인스타 캡션 업로드 전 검수
  python content_inspector.py --post-upload <POST_ID>  # 인스타 업로드 후 검수
  python content_inspector.py --full                   # YouTube+Instagram+Blog 전체 감사
"""
import os
import sys
import json
import re
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, os.path.join(_root, 'ai-team'))

from _shared.env_loader import load_env as _load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.telegram_notifier import send_telegram_message
import hashlib
import random
import urllib.parse

KST = datetime.timezone(datetime.timedelta(hours=9))

SCAN_SLOTS = {
    "morning":   "오전 7시",
    "afternoon": "오후 1시",
    "night":     "오후 9시",
}

SPAM_KEYWORDS = [
    "vevo", "official", "hd", "4k", "free music", "no copyright",
    "royalty free", "best music", "top hits", "playlist",
]

KNOWN_ARTISTS = [
    "bts", "blackpink", "ive", "aespa", "newjeans", "stray kids",
    "twice", "exo", "shinee", "bigbang", "2ne1", "snsd",
]


# ─── YouTube API 헬퍼 ─────────────────────────────────────────────────────────

def _yt_api(endpoint: str, params: dict) -> dict:
    import urllib.request, urllib.parse
    api_key = os.getenv("YOUTUBE_API_KEY", "")
    if not api_key:
        return {}
    params["key"] = api_key
    url = f"https://www.googleapis.com/youtube/v3/{endpoint}?{urllib.parse.urlencode(params)}"
    try:
        with urllib.request.urlopen(url, timeout=15) as r:
            return json.loads(r.read())
    except Exception as e:
        print(f"  [YouTube API] {e}")
        return {}


def _get_video_info(video_id: str) -> dict | None:
    """YouTube API로 영상 메타데이터 수집. API 키 없으면 OAuth 폴백."""
    data = _yt_api("videos", {
        "part": "snippet,statistics,contentDetails",
        "id": video_id,
    })
    if not data.get("items"):
        yt = _get_youtube_write()
        if yt:
            try:
                data = yt.videos().list(
                    part="snippet,statistics,contentDetails",
                    id=video_id,
                ).execute()
            except Exception as e:
                print(f"  [YouTube OAuth] 영상 정보 조회 실패: {e}")
                return None
    items = data.get("items", [])
    if not items:
        return None
    item = items[0]
    sn = item.get("snippet", {})
    stats = item.get("statistics", {})
    dur = item.get("contentDetails", {}).get("duration", "PT0S")
    return {
        "id":           video_id,
        "title":        sn.get("title", ""),
        "description":  sn.get("description", ""),
        "tags":         sn.get("tags", []),
        "category_id":  sn.get("categoryId", ""),
        "channel":      sn.get("channelTitle", ""),
        "published":    sn.get("publishedAt", ""),
        "duration_iso": dur,
        "views":        int(stats.get("viewCount", 0)),
        "likes":        int(stats.get("likeCount", 0)),
        "thumbnail_url": (sn.get("thumbnails", {}).get("high", {}) or
                          sn.get("thumbnails", {}).get("default", {})).get("url", ""),
    }


def _get_channel_videos(max_results: int = 30) -> list[str]:
    """채널의 최근 영상 ID 목록 반환."""
    import pickle
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build

    token_file = os.path.join(_root, ".agent", "credentials", "youtube_token.pickle")
    if not os.path.exists(token_file):
        return []
    try:
        with open(token_file, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
        yt = build("youtube", "v3", credentials=creds)
        ch = yt.channels().list(part="contentDetails", mine=True).execute()
        pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
        res = yt.playlistItems().list(
            part="contentDetails", playlistId=pl_id, maxResults=max_results
        ).execute()
        return [item["contentDetails"]["videoId"] for item in res.get("items", [])]
    except Exception as e:
        print(f"  [가희] 채널 영상 목록 조회 실패: {e}")
        return []


# ─── 분석 모듈 ────────────────────────────────────────────────────────────────

def _parse_duration_seconds(iso: str) -> int:
    """ISO 8601 duration → 초."""
    m = re.match(r"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?", iso)
    if not m:
        return 0
    h, mi, s = (int(x or 0) for x in m.groups())
    return h * 3600 + mi * 60 + s


def _check_metadata(info: dict) -> dict:
    """메타데이터 스팸·정책 위반 체크."""
    title   = info["title"].lower()
    desc    = info["description"].lower()
    tags    = [t.lower() for t in info["tags"]]
    results = {"violations": [], "warnings": []}

    # 태그 스팸 (30개 초과)
    if len(tags) > 30:
        results["violations"].append(f"태그 과다 도배 ({len(tags)}개)")

    # 스팸 키워드 집중 도배
    spam_hits = sum(1 for kw in SPAM_KEYWORDS if kw in title or kw in desc)
    if spam_hits >= 4:
        results["warnings"].append(f"스팸 키워드 {spam_hits}개 감지")

    # 유명 아티스트 사칭 의심
    for artist in KNOWN_ARTISTS:
        if artist in title and info["channel"].lower() not in (artist, f"luna {artist}"):
            results["warnings"].append(f"유명 아티스트 사칭 의심: '{artist}'")
            break

    # 카테고리 10 = 음악인데 설명이 너무 짧음
    if info["category_id"] == "10" and len(info["description"]) < 30:
        results["warnings"].append("음악 카테고리이나 설명 과도하게 짧음")

    # 제목에 음악 키워드 없으면서 음악 카테고리
    music_kw = ["music", "bgm", "lofi", "city pop", "song", "official", "시티팝", "음악", "루나", "luna"]
    if info["category_id"] == "10" and not any(kw in title for kw in music_kw):
        results["warnings"].append("음악 카테고리이나 제목에 음악 관련 키워드 없음")

    return results


def _check_duration(info: dict) -> dict:
    """영상 길이 이상 감지."""
    dur = _parse_duration_seconds(info["duration_iso"])
    results = {"violations": [], "warnings": []}
    if dur < 10:
        results["violations"].append(f"영상 길이 너무 짧음 ({dur}초)")
    elif dur < 30:
        results["warnings"].append(f"영상 길이 짧음 ({dur}초)")
    return results


def _check_duplicate_title(info: dict, all_videos: list[dict]) -> dict:
    """채널 내 제목 중복 감지."""
    import difflib
    results = {"violations": [], "warnings": []}
    title = info["title"].lower()
    for v in all_videos:
        if v["id"] == info["id"]:
            continue
        ratio = difflib.SequenceMatcher(None, title, v["title"].lower()).ratio()
        if ratio >= 0.95:
            results["violations"].append(f"제목 중복 의심 (유사도 {ratio:.0%}): '{v['title'][:40]}'")
        elif ratio >= 0.80:
            results["warnings"].append(f"제목 유사 (유사도 {ratio:.0%}): '{v['title'][:40]}'")
    return results


# ─── 중복 감지·자동 수정 (루나→가희 통합) ──────────────────────────────────────

def _thumb_hash(url: str) -> str | None:
    """썸네일 이미지 MD5 해시 (중복 비교용)."""
    if not url:
        return None
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=10) as r:
            data = r.read()
        return hashlib.md5(data).hexdigest()
    except Exception:
        return None


def _get_youtube_write():
    """YouTube Data API — 쓰기 권한 포함 인증."""
    import pickle
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build
    token_file = os.path.join(_root, ".agent", "credentials", "youtube_token.pickle")
    if not os.path.exists(token_file):
        return None
    try:
        with open(token_file, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
        return build("youtube", "v3", credentials=creds)
    except Exception as e:
        print(f"  [가희-YT 인증] {e}")
        return None


def _generate_unique_title(original: str, used: set) -> str:
    """Ollama로 중복 없는 새 제목 생성. 실패 시 날짜 suffix."""
    prompt = (
        f"다음 유튜브 뮤직비디오 제목을 중복되지 않게 다르게 변형해줘.\n"
        f"원본: {original}\n"
        f"규칙: 고정 공식 없음. 자연스럽게 변형. 한 줄로만 출력."
    )
    for _ in range(3):
        res = lm_chat(prompt, max_tokens=80, temperature=0.9) if lm_available() else None
        if res and res.strip().lower() not in used:
            return res.strip()
    return original + f" | {datetime.datetime.now().strftime('%m.%d')} Edition"


def _generate_new_thumbnail(keyword: str, video_id: str) -> str | None:
    """Pollinations으로 새 썸네일 생성. 실패 시 None."""
    out_dir = os.path.join(_root, ".agent", "skills", "가희_검수관", "tools", "output")
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, f"thumb_{video_id}.jpg")
    variations = [
        f"K-pop city pop aesthetic, {keyword}, neon Seoul night, retro 80s anime style",
        f"Japanese city pop, {keyword}, pastel neon, cinematic retro, fresh composition",
        f"Retro 80s K-pop fusion, {keyword}, golden hour, warm tones, new angle",
    ]
    prompt = random.choice(variations)
    for model in ("flux", "turbo"):
        encoded = urllib.parse.quote(prompt[:400])
        seed = random.randint(1000, 99999)
        url = (f"https://image.pollinations.ai/prompt/{encoded}"
               f"?width=1280&height=720&model={model}&nologo=true&seed={seed}")
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=90) as r:
                data = r.read()
            if len(data) > 10000:
                with open(out_path, "wb") as f:
                    f.write(data)
                print(f"    ✅ 새 썸네일 생성 ({model})")
                return out_path
        except Exception as e:
            print(f"    ⚠️ Pollinations {model}: {e}")
    return None


def _set_youtube_private(video_id: str) -> bool:
    """YouTube 영상을 비공개로 전환. 성공 시 True."""
    youtube = _get_youtube_write()
    if not youtube:
        print(f"    ⚠️ YouTube 인증 없음 — 비공개 전환 불가: {video_id}")
        return False
    try:
        youtube.videos().update(
            part="status",
            body={"id": video_id, "status": {"privacyStatus": "private"}},
        ).execute()
        print(f"    🔒 비공개 전환 완료: {video_id}")
        return True
    except Exception as e:
        print(f"    ❌ 비공개 전환 실패 ({video_id}): {e}")
        return False


def _upload_yt_thumbnail(youtube, video_id: str, img_path: str) -> bool:
    try:
        from googleapiclient.http import MediaFileUpload
        youtube.thumbnails().set(
            videoId=video_id,
            media_body=MediaFileUpload(img_path, mimetype="image/jpeg"),
        ).execute()
        print(f"    ✅ 썸네일 교체: {video_id}")
        return True
    except Exception as e:
        print(f"    ❌ 썸네일 업로드 실패: {e}")
        return False


def _update_yt_metadata(youtube, video_id: str, title: str, description: str = None) -> bool:
    try:
        body: dict = {"id": video_id, "snippet": {"title": title, "categoryId": "10"}}
        if description:
            body["snippet"]["description"] = description
        youtube.videos().update(part="snippet", body=body).execute()
        print(f"    ✅ 메타데이터 수정: {video_id}")
        return True
    except Exception as e:
        print(f"    ❌ 메타데이터 수정 실패: {e}")
        return False


def _find_channel_duplicates(videos: list[dict]) -> dict:
    """채널 전체 영상 제목·설명·썸네일 중복 그룹 탐지."""
    import difflib
    title_groups: dict[str, list] = {}
    desc_groups:  dict[str, list] = {}
    thumb_groups: dict[str, list] = {}

    print("  썸네일 해시 비교 중...")
    for v in videos:
        clean = re.sub(r"\[.*?\]|\(.*?\)", "", v["title"]).strip().lower()
        title_groups.setdefault(clean, []).append(v)

        desc_key = v["description"][:100].strip().lower()
        if desc_key:
            desc_groups.setdefault(desc_key, []).append(v)

        h = _thumb_hash(v.get("thumbnail_url", ""))
        if h:
            v["_thumb_hash"] = h
            thumb_groups.setdefault(h, []).append(v)

    return {
        "titles":      {k: vs for k, vs in title_groups.items() if len(vs) > 1},
        "descriptions":{k: vs for k, vs in desc_groups.items()  if len(vs) > 1},
        "thumbnails":  {k: vs for k, vs in thumb_groups.items() if len(vs) > 1},
    }


def _fix_channel_duplicates(videos: list[dict]) -> dict:
    """중복 감지 → YouTube API로 자동 수정. 수정 결과 반환."""
    dupes = _find_channel_duplicates(videos)
    dup_titles = dupes["titles"]
    dup_descs  = dupes["descriptions"]
    dup_thumbs = dupes["thumbnails"]

    total = len(dup_titles) + len(dup_descs) + len(dup_thumbs)
    print(f"  중복 제목:{len(dup_titles)} | 설명:{len(dup_descs)} | 썸네일:{len(dup_thumbs)}")

    if total == 0:
        return {"fixed": 0, "details": []}

    youtube = _get_youtube_write()
    if not youtube:
        print("  ⚠️ YouTube 인증 없음 — 수정 건너뜀")
        return {"fixed": 0, "details": [], "error": "auth_missing"}

    all_titles    = {v["title"].lower() for v in videos}
    fixed_count   = 0
    already_fixed: set[str] = set()
    details: list[dict] = []

    # ── 중복 제목 수정 ──────────────────────────────────────────────
    for _key, group in dup_titles.items():
        print(f"\n  ⚠️ 제목 중복 ({len(group)}개): '{group[0]['title'][:50]}'")
        for v in group[1:]:
            kw_m = re.search(r"LUNA\s*-\s*(.+?)\s*[\[\(]", v["title"])
            kw   = kw_m.group(1).strip() if kw_m else v["title"][:30]
            new_title = _generate_unique_title(v["title"], all_titles)
            print(f"    → [{v['id']}] '{v['title'][:35]}' → '{new_title[:35]}'")
            thumb_path = _generate_new_thumbnail(kw, v["id"])
            if _update_yt_metadata(youtube, v["id"], title=new_title):
                all_titles.add(new_title.lower())
                already_fixed.add(v["id"])
                fixed_count += 1
                details.append({"id": v["id"], "fix": "title", "new": new_title})
            if thumb_path:
                _upload_yt_thumbnail(youtube, v["id"], thumb_path)

    # ── 중복 썸네일 수정 (제목 수정 영상 제외) ─────────────────────
    for _key, group in dup_thumbs.items():
        to_fix = [v for v in group[1:] if v["id"] not in already_fixed]
        if not to_fix:
            continue
        print(f"\n  ⚠️ 썸네일 중복 ({len(group)}개): hash={_key[:8]}...")
        for v in to_fix:
            kw_m = re.search(r"LUNA\s*-\s*(.+?)\s*[\[\(]", v["title"])
            kw   = kw_m.group(1).strip() if kw_m else "City Pop"
            print(f"    → [{v['id']}] '{v['title'][:35]}' 썸네일 교체")
            thumb_path = _generate_new_thumbnail(kw, v["id"])
            if thumb_path and _upload_yt_thumbnail(youtube, v["id"], thumb_path):
                already_fixed.add(v["id"])
                fixed_count += 1
                details.append({"id": v["id"], "fix": "thumbnail"})

    # ── 중복 설명 수정 ────────────────────────────────────────────
    today = datetime.datetime.now().strftime("%Y.%m.%d")
    for _key, group in dup_descs.items():
        print(f"\n  ⚠️ 설명 중복 ({len(group)}개): '{_key[:40]}...'")
        for v in group[1:]:
            if v["id"] in already_fixed:
                continue
            new_desc = f"[Updated {today}]\n" + v["description"]
            if _update_yt_metadata(youtube, v["id"], title=v["title"], description=new_desc):
                fixed_count += 1
                details.append({"id": v["id"], "fix": "description"})

    return {"fixed": fixed_count, "details": details}


def _analyze_with_ollama(info: dict) -> dict:
    """Ollama로 콘텐츠 정책·품질 종합 분석."""
    if not lm_available():
        return {"warnings": ["Ollama 미연결 — AI 분석 건너뜀"]}

    prompt = (
        f"유튜브 음악 영상 콘텐츠를 정책 위반 관점에서 분석해줘.\n\n"
        f"제목: {info['title']}\n"
        f"설명: {info['description'][:300]}\n"
        f"태그: {', '.join(info['tags'][:15])}\n"
        f"채널: {info['channel']}\n\n"
        f"다음 항목을 확인하고 JSON으로만 반환:\n"
        f'{{"violations": ["위반1"], "warnings": ["경고1"], "is_spam": false, '
        f'"is_impersonation": false, "metadata_consistent": true, "comment": "한 줄 요약"}}'
    )

    raw = lm_chat(prompt, json_mode=True, max_tokens=300, temperature=0.3)
    if raw:
        try:
            return json.loads(raw.strip())
        except Exception:
            pass
    return {}


# ─── 판정 로직 ────────────────────────────────────────────────────────────────

def _determine_verdict(violations: list, warnings: list, confidence_base: float) -> tuple:
    """violations/warnings → status, action, risk_level, confidence 결정."""
    if violations:
        status   = "REJECT"
        risk     = "HIGH"
        action   = "TAKEDOWN"
        conf     = min(0.95, confidence_base + 0.2)
    elif len(warnings) >= 3:
        status   = "REVIEW"
        risk     = "MEDIUM"
        action   = "NOTIFY_USER"
        conf     = confidence_base
    elif warnings:
        status   = "REVIEW"
        risk     = "LOW"
        action   = "NOTIFY_USER"
        conf     = max(0.5, confidence_base - 0.1)
    else:
        status   = "PASS"
        risk     = "LOW"
        action   = "NONE"
        conf     = confidence_base
    return status, action, risk, round(conf, 2)


def inspect_video(video_id: str, mode: str = "EXISTING_CONTENT",
                  all_videos: list[dict] = None) -> dict:
    """단건 영상 검수 — JSON 판정 결과 반환."""
    info = _get_video_info(video_id)
    if not info:
        return {
            "content_id": video_id,
            "inspection_mode": mode,
            "status": "REVIEW",
            "action_required": "NOTIFY_USER",
            "confidence": 0.0,
            "risk_level": "MEDIUM",
            "violations": ["영상 정보 수집 실패"],
            "warnings": [],
            "analysis": {},
            "review_comment": "YouTube API로 영상 정보를 가져올 수 없습니다.",
        }

    violations: list = []
    warnings:   list = []

    # 1. 메타데이터 체크
    meta = _check_metadata(info)
    violations += meta["violations"]
    warnings   += meta["warnings"]

    # 2. 길이 체크
    dur = _check_duration(info)
    violations += dur["violations"]
    warnings   += dur["warnings"]

    # 3. 제목 중복 체크
    if all_videos:
        dup = _check_duplicate_title(info, all_videos)
        violations += dup["violations"]
        warnings   += dup["warnings"]

    # 4. Ollama AI 분석
    ai = _analyze_with_ollama(info)
    violations += ai.get("violations", [])
    warnings   += ai.get("warnings", [])
    if ai.get("is_spam"):
        violations.append("AI 분석: 스팸 콘텐츠 의심")
    if ai.get("is_impersonation"):
        violations.append("AI 분석: 아티스트 사칭 의심")

    status, action, risk, conf = _determine_verdict(violations, warnings, 0.75)

    # 모드별 조치 조정
    if mode == "NEW_UPLOAD" and status == "REJECT":
        action = "NONE"  # 신규 업로드는 즉시 반려 (삭제 대신)

    comment = ai.get("comment", "")
    if not comment:
        if status == "PASS":
            comment = "정상 콘텐츠로 판정됩니다."
        elif status == "REVIEW":
            comment = f"검토 필요: {warnings[0] if warnings else '경고 항목 확인 요망'}"
        else:
            comment = f"정책 위반 감지: {violations[0] if violations else '위반 항목 확인'}"

    return {
        "content_id":       video_id,
        "inspection_mode":  mode,
        "status":           status,
        "action_required":  action,
        "confidence":       conf,
        "risk_level":       risk,
        "violations":       violations,
        "warnings":         warnings,
        "analysis": {
            "audio_presence":        {"note": "실제 오디오 파일 없음 — 메타데이터 기반 추론"},
            "audio_quality":         {"note": "실제 오디오 파일 없음 — 메타데이터 기반 추론"},
            "fingerprint_similarity": {"note": "Fingerprint DB 미연동"},
            "visual_audio_context":  {"metadata_consistent": ai.get("metadata_consistent", True)},
            "metadata_consistency":  {"tag_count": len(info["tags"]), "desc_length": len(info["description"])},
            "policy_checks":         {"spam": ai.get("is_spam", False), "impersonation": ai.get("is_impersonation", False)},
        },
        "review_comment": comment,
    }


# ─── 메인 실행 ────────────────────────────────────────────────────────────────

def run_scan(target_id: str = None, new_only: bool = False):
    """채널 전체 스캔 또는 단건 검수."""
    _load_env()
    kst_now = datetime.datetime.now(KST).strftime("%Y-%m-%d %H:%M KST")
    print(f"🔍 [가희] 콘텐츠 검수 시작 ({kst_now})")

    if target_id:
        # 단건
        mode   = "NEW_UPLOAD" if new_only else "EXISTING_CONTENT"
        result = inspect_video(target_id, mode)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        _report_if_needed([result], kst_now)
        return

    # 채널 전체 스캔
    video_ids = _get_channel_videos(max_results=30)
    if not video_ids:
        print("  [가희] 채널 영상 없음 또는 인증 필요")
        return

    print(f"  총 {len(video_ids)}개 영상 검수 중...")

    # 전체 영상 정보 미리 수집 (중복 제목 비교용)
    all_infos = []
    for vid in video_ids:
        info = _get_video_info(vid)
        if info:
            all_infos.append(info)

    results = []
    for info in all_infos:
        mode = "NEW_UPLOAD" if new_only else "EXISTING_CONTENT"
        result = inspect_video(info["id"], mode, all_infos)
        status = result["status"]
        icon   = "✅" if status == "PASS" else ("⚠️" if status == "REVIEW" else "❌")
        print(f"  {icon} [{status}] {info['title'][:50]}")
        results.append(result)

    _report_if_needed(results, kst_now)
    print(f"\n✅ [가희] 검수 완료 — PASS:{sum(1 for r in results if r['status']=='PASS')} "
          f"REVIEW:{sum(1 for r in results if r['status']=='REVIEW')} "
          f"REJECT:{sum(1 for r in results if r['status']=='REJECT')}")


_INSPECT_LOG = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    *[".."] * 10,  # 임시 — 실제 경로는 _root 기준으로 아래에서 재설정
    ".agent", "memory", "gahee_inspection_log.jsonl"
)

def _get_inspect_log_path() -> str:
    root = os.path.dirname(os.path.abspath(__file__))
    for _ in range(6):
        if os.path.isdir(os.path.join(root, ".agent")):
            break
        root = os.path.dirname(root)
    return os.path.join(root, ".agent", "memory", "gahee_inspection_log.jsonl")


def _save_inspection_results(results: list, timestamp: str):
    """REVIEW/REJECT 판정 결과를 jsonl 로그에 누적 저장 (fix_issues.py 자동 로드용)."""
    log_path = _get_inspect_log_path()
    issues = [r for r in results if r["status"] in ("REVIEW", "REJECT")]
    if not issues:
        return
    os.makedirs(os.path.dirname(log_path), exist_ok=True)

    # 기존 로그 읽어 중복 체크
    existing_ids: set = set()
    if os.path.exists(log_path):
        try:
            with open(log_path) as f:
                for line in f:
                    rec = json.loads(line.strip())
                    if not rec.get("resolved"):
                        existing_ids.add(rec.get("content_id", ""))
        except Exception:
            pass

    new_count = 0
    with open(log_path, "a", encoding="utf-8") as f:
        for r in issues:
            cid = r.get("content_id", "")
            if cid in existing_ids:
                continue  # 이미 기록된 미해결 이슈는 중복 추가하지 않음
            record = {
                "logged_at":   timestamp,
                "platform":    "youtube",
                "content_id":  cid,
                "title":       r.get("analysis", {}).get("title", ""),
                "status":      r["status"],
                "violations":  r.get("violations", []),
                "warnings":    r.get("warnings", []),
                "fix_type":    _guess_fix_type(r),
                "resolved":    False,
            }
            f.write(json.dumps(record, ensure_ascii=False) + "\n")
            new_count += 1

    if new_count:
        print(f"  [가희 지식 저장] {new_count}건 → gahee_inspection_log.jsonl")


def _guess_fix_type(result: dict) -> str:
    """판정 결과에서 적절한 fix_type 추론."""
    violations = " ".join(result.get("violations", []))
    warnings   = " ".join(result.get("warnings", []))
    combined   = (violations + " " + warnings).lower()
    if "쇼츠" in combined or "#shorts" in combined or "shorts" in combined:
        return "make_private_shorts_violation"
    if "luna" in combined and "접두어" in combined:
        return "fix_luna_title_prefix"
    if "ai 생성" in combined or "ai-generated" in combined or "미공시" in combined:
        return "add_music_keyword_and_ai_disclosure"
    return "add_music_keyword"


def _auto_fix_issues():
    """REVIEW/REJECT 판정 즉시 자동 수정 실행 (fix_issues.py 로직 인라인 호출)."""
    import importlib.util, pathlib
    fix_path = pathlib.Path(__file__).parent / "fix_issues.py"
    if not fix_path.exists():
        print("  ⚠️ fix_issues.py 없음 — 자동 수정 건너뜀")
        return

    try:
        spec = importlib.util.spec_from_file_location("fix_issues", str(fix_path))
        fix_mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fix_mod)
        fix_mod.main()
    except Exception as e:
        print(f"  ❌ 자동 수정 실패: {e}")


def _report_if_needed(results: list, timestamp: str):
    """REVIEW/REJECT 있으면 텔레그램 보고 + jsonl 지식 저장 + 즉시 자동 수정."""
    issues = [r for r in results if r["status"] in ("REVIEW", "REJECT")]
    if not issues:
        return

    _save_inspection_results(results, timestamp)  # 지식 저장

    lines = [f"🔍 [가희] 콘텐츠 검수 결과 ({timestamp})\n"]
    for r in issues:
        icon   = "❌" if r["status"] == "REJECT" else "⚠️"
        action = r["action_required"]
        lines.append(f"{icon} [{r['status']}] {r['content_id']}")
        lines.append(f"   조치: {action} | 신뢰도: {r['confidence']:.0%}")
        if r["violations"]:
            lines.append(f"   위반: {r['violations'][0]}")
        if r["warnings"] and not r["violations"]:
            lines.append(f"   경고: {r['warnings'][0]}")
        lines.append(f"   코멘트: {r['review_comment'][:80]}")
        lines.append("")
    lines.append("→ 자동 수정 즉시 실행 중...")

    send_telegram_message("\n".join(lines))

    # 검수 직후 즉시 수정 실행
    _auto_fix_issues()



# ─── 전체 에이전트 작업물 검수 + 수정 지시 ───────────────────────────────────

def _check_blog_posts() -> list[dict]:
    import pickle, re
    token_file = os.path.join(_root, ".agent", "credentials", "token_sukja.pickle")
    BLOG_ID    = "2377962046418550821"
    results    = []
    if not os.path.exists(token_file):
        return results
    try:
        from google.auth.transport.requests import Request
        from googleapiclient.discovery import build
        with open(token_file, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
        service = build("blogger", "v3", credentials=creds)
        res = service.posts().list(blogId=BLOG_ID, maxResults=10, status=["LIVE"]).execute()
        for p in res.get("items", []):
            title   = p.get("title", "")
            content = re.sub(r"<[^>]+>", " ", p.get("content", ""))
            word_count = len(content.split())
            issues  = []
            if word_count < 500:
                issues.append(f"본문 심각하게 짧음 (위반: {word_count}단어)")
            elif word_count < 1500:
                issues.append(f"본문 분량 미달 (경고: {word_count}/1500단어)")
            if not title:
                issues.append("제목 없음")
            results.append({
                "platform": "blog", "id": p["id"], "title": title[:50],
                "word_count": word_count, "issues": issues,
                "instruction": f"블로그 포스팅 '{title[:30]}' 본문을 1000단어 이상으로 보강해줘." if issues else "",
            })
    except Exception as e:
        print(f"  [가희-블로그] {e}")
    return results


def _check_instagram_posts() -> list[dict]:
    """아린 인스타 포스팅 검수 — 금지 규칙 포함."""
    import urllib.request, difflib
    token      = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    account_id = os.getenv("INSTAGRAM_ACCOUNT_ID", "")
    results    = []
    if not token or not account_id:
        return results

    # ⛔ CEO 금지 규칙: 캡션/해시태그에 AI·미래·기술 키워드 포함 금지
    _BANNED_KEYWORDS = [
        "미래", "인공지능", "ai", "기계", "테크", "로봇", "첨단기술",
        "4차산업", "딥러닝", "머신러닝", "ai 생성", "인공지능이 만든",
        "오늘의 ai", "체험해보세요", "경험해보세요",
        "lofi", "lo-fi", "chill beats", "study beats"
    ]

    try:
        url = (f"https://graph.instagram.com/v23.0/{account_id}/media"
               f"?fields=id,caption,timestamp&limit=10&access_token={token}")
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        seen_captions = []
        for p in data.get("data", []):
            caption = p.get("caption", "")
            lower   = caption.lower()
            issues  = []

            if not caption:
                issues.append("캡션 없음")
            elif len(caption) < 50:
                issues.append("캡션 너무 짧음 (50자 미만)")

            # 금지 키워드 검사
            hit = [kw for kw in _BANNED_KEYWORDS if kw in lower]
            if hit:
                issues.append(f"금지 키워드 포함: {', '.join(hit)}")

            # 중복 캡션 검사
            for prev in seen_captions:
                if difflib.SequenceMatcher(None, lower, prev.lower()).ratio() > 0.85:
                    issues.append("중복 캡션 감지 (85% 이상 유사)")
                    break
            seen_captions.append(caption)

            results.append({
                "platform": "instagram", "id": p["id"],
                "caption": caption[:80], "issues": issues,
                "action": "아린" if issues else None,
                "instruction": f"인스타 포스팅 ID {p['id']} 위반: {issues}" if issues else "",
            })
    except Exception as e:
        print(f"  [가희-인스타] {e}")
    return results


def inspect_caption(caption: str) -> dict:
    """인스타 캡션 업로드 전 사전 검수. 결과: {'pass': bool, 'issues': list[str]}"""
    _BANNED = [
        "미래", "인공지능", "ai", "기계", "테크", "로봇", "첨단기술",
        "4차산업", "딥러닝", "머신러닝", "ai 생성", "인공지능이 만든",
        "오늘의 ai", "체험해보세요", "경험해보세요", "lofi", "lo-fi",
        "chill beats", "study beats",
    ]
    issues = []
    lower = caption.lower()
    if not caption.strip():
        issues.append("캡션 없음")
    elif len(caption) < 50:
        issues.append(f"캡션 너무 짧음 ({len(caption)}자)")
    hits = [kw for kw in _BANNED if kw in lower]
    if hits:
        issues.append(f"금지 키워드: {', '.join(hits)}")
    return {"pass": len(issues) == 0, "issues": issues}


def inspect_post_upload(post_id: str) -> dict:
    """업로드 후 실제 게시물 내용 검수. 결과: {'pass': bool, 'issues': list[str]}"""
    import urllib.request
    _load_env()
    token = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    if not token:
        return {"pass": True, "issues": ["토큰 없음 — 검수 생략"]}
    try:
        url = (f"https://graph.instagram.com/v23.0/{post_id}"
               f"?fields=id,caption&access_token={token}")
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        caption = data.get("caption", "")
        result = inspect_caption(caption)
        result["post_id"] = post_id
        return result
    except Exception as e:
        return {"pass": False, "issues": [f"게시물 조회 실패: {e}"]}


def _check_scheduled_videos() -> list[dict]:
    """예약 게시(publishAt 설정된) YouTube 영상도 검수."""
    import pickle
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build

    token_file = os.path.join(_root, ".agent", "credentials", "youtube_token.pickle")
    if not os.path.exists(token_file):
        return []
    results = []
    try:
        with open(token_file, "rb") as f:
            creds = pickle.load(f)
        if creds.expired and creds.refresh_token:
            creds.refresh(Request())
        yt = build("youtube", "v3", credentials=creds)
        # uploads 플레이리스트에서 최근 50개 영상 ID 수집
        ch = yt.channels().list(part="contentDetails", mine=True).execute()
        pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
        pl_res = yt.playlistItems().list(
            part="contentDetails", playlistId=pl_id, maxResults=50
        ).execute()
        video_ids = [item["contentDetails"]["videoId"] for item in pl_res.get("items", [])]
        if not video_ids:
            return []
        # 영상 상세(status 포함) 일괄 조회
        res = yt.videos().list(
            part="snippet,status,contentDetails",
            id=",".join(video_ids),
        ).execute()
        # status.privacyStatus == "private" + publishAt 있으면 예약 게시
        for item in res.get("items", []):
            status = item.get("status", {})
            publish_at = status.get("publishAt", "")
            privacy = status.get("privacyStatus", "")
            if privacy == "private" and publish_at:
                sn = item.get("snippet", {})
                dur = item.get("contentDetails", {}).get("duration", "PT0S")
                results.append({
                    "id": item["id"],
                    "title": sn.get("title", ""),
                    "description": sn.get("description", ""),
                    "tags": sn.get("tags", []),
                    "category_id": sn.get("categoryId", "10"),
                    "channel": sn.get("channelTitle", ""),
                    "published": sn.get("publishedAt", ""),
                    "duration_iso": dur,
                    "views": 0,
                    "likes": 0,
                    "scheduled_at": publish_at,
                })
    except Exception as e:
        print(f"  [가희-예약영상] {e}")
    return results


def run_full_audit():
    """YouTube(공개+예약) + 인스타 + 블로그 전체 검수 → 각 에이전트에 수정 지시."""
    _load_env()
    kst_now = datetime.datetime.now(KST).strftime("%Y-%m-%d %H:%M KST")
    print(f"🔎 [가희] 전체 에이전트 작업물 검수 시작 ({kst_now})\n")

    all_results = []

    # 1. YouTube 공개 영상 (루나) — 품질 검수
    print("── YouTube 공개 영상 (루나) 검수 중...")
    video_ids = _get_channel_videos(max_results=30)
    yt_infos  = [_get_video_info(v) for v in video_ids if v]
    yt_infos  = [v for v in yt_infos if v]
    yt_results = [inspect_video(v["id"], "EXISTING_CONTENT", yt_infos) for v in yt_infos]
    for r in yt_results:
        icon = "✅" if r["status"] == "PASS" else ("⚠️" if r["status"] == "REVIEW" else "❌")
        title = next((v["title"][:40] for v in yt_infos if v["id"] == r["content_id"]), r["content_id"])
        print(f"  {icon} [{r['status']}] {title}")
        privated_note = ""
        if r["status"] != "PASS":
            ok = _set_youtube_private(r["content_id"])
            privated_note = " [비공개 전환됨]" if ok else " [비공개 전환 실패]"
        all_results.append({**r, "platform": "youtube", "agent": "루나",
                             "instruction": f"YouTube 영상 {r['content_id']}{privated_note} 수정 필요: {r['violations'] + r['warnings']}" if r["status"] != "PASS" else ""})

    # 1c. YouTube 채널 전체 중복 감지·자동 수정 (제목/설명/썸네일)
    print("\n── YouTube 중복 감지·자동 수정 중...")
    dup_result = _fix_channel_duplicates(yt_infos)
    if dup_result["fixed"] > 0:
        all_results.append({
            "platform": "youtube_dup", "agent": "루나",
            "issues": [f"중복 {dup_result['fixed']}건 자동 수정 완료"],
            "instruction": "",
        })
        print(f"  ✅ 중복 {dup_result['fixed']}건 자동 수정")

    # 1b. YouTube 예약 게시 영상 검수
    print("\n── YouTube 예약 게시 영상 (루나) 검수 중...")
    scheduled_infos = _check_scheduled_videos()
    if scheduled_infos:
        all_yt_infos = yt_infos + scheduled_infos
        for sched in scheduled_infos:
            r = inspect_video(sched["id"], "NEW_UPLOAD", all_yt_infos)
            icon = "✅" if r["status"] == "PASS" else ("⚠️" if r["status"] == "REVIEW" else "❌")
            print(f"  {icon} [예약:{sched['scheduled_at'][:10]}] [{r['status']}] {sched['title'][:40]}")
            all_results.append({**r, "platform": "youtube_scheduled", "agent": "루나",
                                 "scheduled_at": sched["scheduled_at"],
                                 "instruction": f"예약 영상 {r['content_id']} 검토 필요: {r['violations'] + r['warnings']}" if r["status"] != "PASS" else ""})
    else:
        print("  예약 게시 영상 없음")

    # 2. Instagram (아린)
    print("\n── Instagram (아린) 검수 중...")
    insta = _check_instagram_posts()
    for r in insta:
        icon = "✅" if not r["issues"] else "⚠️"
        print(f"  {icon} {r['caption'][:40]}... — {r['issues'] or '정상'}")
        all_results.append(r)

    blog = _check_blog_posts()
    for r in blog:
        icon = "✅" if not r["issues"] else "⚠️"
        print(f"  {icon} {r['title']} ({r.get('word_count',0)}단어) — {r['issues'] or '정상'}")
        all_results.append(r)

    # 요약 보고
    issues_found = [r for r in all_results if (r.get("issues") or r.get("violations") or r.get("warnings"))]
    pass_count   = len(all_results) - len(issues_found)

    lines = [f"🔎 [가희 → 영숙] 콘텐츠 품질 검수 리포트 ({kst_now})\n",
             f"총 {len(all_results)}건 | ✅ 정상 {pass_count}건 | ⚠️/❌ 문제 {len(issues_found)}건\n"]

    critical_issues = []
    for r in issues_found:
        platform = r.get("platform", "?")
        agent    = r.get("agent", r.get("action", "?"))
        detail   = (r.get("violations") or r.get("warnings") or r.get("issues") or [])[:1]
        # 자동 수정된 항목은 영숙이가 처리, 미해결건만 예원에게
        can_autofix = "YES" if platform == "youtube_dup" else "NO"
        
        lines.append(f"⚠️ [{platform}→{agent}] {detail[0]} (자동수정: {can_autofix})")
        if can_autofix == "NO":
            critical_issues.append(f"[{platform}] {agent}: {detail[0]}")

    if critical_issues:
        lines.append("\n📬 **비서 영숙님, 위 위반 사항들을 CEO 예원님께 보고하여 수정 지시를 요청해주세요.**")
    else:
        lines.append("\n✅ 모든 문제가 자동 수정되었거나 경미합니다. 영숙님이 요약 보고 후 종료해주세요.")

    msg = "\n".join(lines)
    print(f"\n{msg}")
    if issues_found:
        send_telegram_message(msg)

    return all_results


# ─── 수정 요청 발송 ───────────────────────────────────────────────────────────

def request_correction(platform: str, agent: str, content_id: str,
                       issues: list, instruction: str):
    """문제 감지 시 담당 에이전트에게 텔레그램 수정 요청 발송."""
    is_critical = any(
        kw in str(i).lower() for i in issues
        for kw in ("위반", "reject", "사칭", "중복", "금지")
    )
    icon = "🔴" if is_critical else "🟡"
    msg = (
        f"{icon} [가희 → {agent}] 수정 요청\n"
        f"플랫폼: {platform} | ID: {content_id}\n"
        f"문제: {', '.join(str(i) for i in issues[:2])}\n"
        f"지시: {instruction}"
    )
    send_telegram_message(msg)
    print(f"  📨 수정 요청 발송 → {agent}: {issues[0] if issues else ''}")


# ─── 하루 3회 정기 검수 ───────────────────────────────────────────────────────

def run_scheduled_scan(slot: str = "morning"):
    """
    하루 3회 정기 검수 실행.
    slot: 'morning'(07:00) | 'afternoon'(13:00) | 'night'(21:00) KST
    YouTube(공개+예약) + Instagram 동시 검수 → 문제 발견 시 즉시 수정 요청.
    """
    _load_env()
    kst_now    = datetime.datetime.now(KST).strftime("%Y-%m-%d %H:%M KST")
    slot_label = SCAN_SLOTS.get(slot, slot)
    print(f"\n{'='*55}")
    print(f"🔍 [가희] {slot_label} 정기 검수 시작 ({kst_now})")
    print(f"{'='*55}\n")

    correction_requests = []

    # ── 1. YouTube 공개 영상 검수 ────────────────────────────────────────────
    print("── YouTube 공개 영상 검수 중...")
    video_ids = _get_channel_videos(max_results=20)
    yt_infos  = [_get_video_info(v) for v in video_ids if v]
    yt_infos  = [v for v in yt_infos if v]
    for v in yt_infos:
        r    = inspect_video(v["id"], "EXISTING_CONTENT", yt_infos)
        icon = "✅" if r["status"] == "PASS" else ("⚠️" if r["status"] == "REVIEW" else "❌")
        print(f"  {icon} [{r['status']}] {v['title'][:45]}")
        if r["status"] != "PASS":
            issues = r["violations"] + r["warnings"]
            # 🔒 문제 있는 영상 즉시 비공개 전환
            privated = _set_youtube_private(v["id"])
            status_note = "비공개 전환 완료" if privated else "비공개 전환 실패(수동 처리 필요)"
            correction_requests.append((
                "YouTube", "루나", v["id"], issues,
                f"'{v['title'][:30]}' ({v['id']}) {status_note} — 수정 후 재공개 요망: {r['review_comment'][:60]}"
            ))

    # ── 2. YouTube 예약 영상 사전 검수 ───────────────────────────────────────
    print("\n── YouTube 예약 업로드 사전 검수 중...")
    scheduled = _check_scheduled_videos()
    if scheduled:
        all_yt = yt_infos + scheduled
        for s in scheduled:
            r    = inspect_video(s["id"], "NEW_UPLOAD", all_yt)
            icon = "✅" if r["status"] == "PASS" else ("⚠️" if r["status"] == "REVIEW" else "❌")
            print(f"  {icon} [예약:{s['scheduled_at'][:10]}] [{r['status']}] {s['title'][:40]}")
            if r["status"] != "PASS":
                issues = r["violations"] + r["warnings"]
                # 🔒 예약 영상도 비공개 유지 (예약 취소 효과)
                _set_youtube_private(s["id"])
                correction_requests.append((
                    "YouTube(예약)", "루나", s["id"], issues,
                    f"예약 영상 '{s['title'][:30]}' 비공개 전환 — 수정 후 재예약 필요: {r['review_comment'][:60]}"
                ))
    else:
        print("  예약 게시 영상 없음")

    # ── 3. Instagram 검수 ─────────────────────────────────────────────────────
    print("\n── Instagram 검수 중...")
    insta = _check_instagram_posts()
    if insta:
        for p in insta:
            icon = "✅" if not p["issues"] else "⚠️"
            print(f"  {icon} {p['caption'][:40]}... — {p['issues'] or '정상'}")
            if p["issues"]:
                # Instagram API는 비공개 전환 미지원 → 아린에게 수동 삭제/수정 요청
                correction_requests.append((
                    "Instagram", "아린", p["id"], p["issues"],
                    f"인스타 포스팅 ID {p['id']} 즉시 삭제 또는 수정 후 재업로드 요망: {'; '.join(p['issues'])}"
                ))
    else:
        print("  Instagram API 미연결 또는 게시물 없음")

    # ── 4. 수정 요청 발송 ─────────────────────────────────────────────────────
    print(f"\n{'─'*40}")
    if correction_requests:
        print(f"📨 수정 요청 {len(correction_requests)}건 발송 중...")
        for platform, agent, cid, issues, instruction in correction_requests:
            request_correction(platform, agent, cid, issues, instruction)

        summary = (
            f"📋 [가희] {slot_label} 정기 검수 완료 ({kst_now})\n"
            f"수정 요청 {len(correction_requests)}건 발송\n\n"
            + "\n".join(
                f"→ [{p}→{a}] {iss[0] if iss else ''}"
                for p, a, _, iss, _ in correction_requests
            )
        )
        send_telegram_message(summary)
    else:
        msg = f"✅ [가희] {slot_label} 정기 검수 완료 ({kst_now}) — 전체 정상, 수정 요청 없음"
        print(msg)
        send_telegram_message(msg)

    print(f"\n{'='*55}")
    print(f"✅ [가희] {slot_label} 검수 종료")
    print(f"{'='*55}\n")
    return correction_requests


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    args = sys.argv[1:]

    if "--full" in args:
        run_full_audit()

    elif "--schedule" in args:
        idx  = args.index("--schedule")
        slot = args[idx + 1] if idx + 1 < len(args) else "morning"
        run_scheduled_scan(slot)

    elif "--pre-upload" in args:
        # 인스타 캡션 업로드 전 검수
        idx     = args.index("--pre-upload")
        caption = args[idx + 1] if idx + 1 < len(args) else ""
        _load_env()
        result  = inspect_caption(caption)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if not result["pass"]:
            request_correction(
                "Instagram", "아린", "PRE_UPLOAD", result["issues"],
                f"캡션 업로드 전 검수 실패: {'; '.join(result['issues'])} — 수정 후 재시도 요망"
            )

    elif "--post-upload" in args:
        # 인스타 업로드 후 검수
        idx     = args.index("--post-upload")
        post_id = args[idx + 1] if idx + 1 < len(args) else ""
        _load_env()
        result  = inspect_post_upload(post_id)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if not result["pass"]:
            request_correction(
                "Instagram", "아린", post_id, result["issues"],
                f"업로드 후 검수 실패 (ID:{post_id}): {'; '.join(result['issues'])} — 즉시 수정 요망"
            )

    else:
        target   = None
        new_only = "--new" in args
        if "--id" in args:
            idx = args.index("--id")
            if idx + 1 < len(args):
                target = args[idx + 1]
        run_scan(target_id=target, new_only=new_only)
