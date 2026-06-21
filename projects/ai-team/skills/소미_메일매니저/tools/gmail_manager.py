#!/usr/bin/env python3
"""
소미 — Gmail Inbox Zero 에이전트
받은편지함을 자동 분류·정리한다. 삭제 없음. 애매하면 REVIEW.
"""
import os
import sys
import json
import pickle
import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env import load_env
from _shared.llm import text as llm_text
from _shared.notify import send, report

load_env()

AGENT_NAME = "소미"

# ── 인증 경로 ──────────────────────────────────────────────────────────────
_PROJECT_ROOT = os.path.abspath(os.path.join(_ai_team_root, "..", ".."))
TOKEN_FILE  = os.path.join(_PROJECT_ROOT, "gmail_token.pickle")
SECRET_FILE = os.path.join(_PROJECT_ROOT, "gmail_client_secret.json")
SCOPES      = ["https://www.googleapis.com/auth/gmail.modify"]

# ── 분류 라벨 ─────────────────────────────────────────────────────────────
LABEL_NAMES  = ["IMPORTANT", "REVIEW", "FINANCE", "DEV", "SHOPPING", "PROMOTION"]
KEEP_IN_INBOX = {"IMPORTANT", "REVIEW"}

# ── 시스템 프롬프트 ───────────────────────────────────────────────────────
SYSTEM_PROMPT = """당신은 Gmail Inbox Zero 정리 에이전트입니다.

목표:
- 받은편지함을 자동 정리한다.
- 중요한 메일은 절대 놓치지 않는다.
- AI 사용량과 토큰 사용량을 최소화한다.
- 삭제는 하지 않는다.
- 애매하면 보수적으로 판단한다.

분류 라벨: IMPORTANT | REVIEW | FINANCE | DEV | SHOPPING | PROMOTION

라벨 정의:
IMPORTANT — 결제 실패, 보안 알림, 계정 문제, 고객 문의, 서비스 장애, 업무 관련 중요 연락
REVIEW    — 중요 여부 불확실, 사람이 확인 필요
FINANCE   — 거래소, 은행, 카드, 결제, 세금, 투자, 자산 관련
DEV       — Github, Vercel, Cloudflare, AWS, GCP, Azure, OpenAI, Anthropic, 개발 도구, 서버 알림
SHOPPING  — 주문, 배송, 영수증, 구매 내역
PROMOTION — 광고, 이벤트, 마케팅, 뉴스레터, 프로모션

절대 규칙:
1. from, subject, snippet 만으로 판단한다.
2. 금융 관련 메일은 절대 PROMOTION 으로 분류하지 않는다.
3. 로그인 알림, 비밀번호 변경, 2FA, 결제 실패, 서비스 장애 → 무조건 IMPORTANT.
4. 확신이 없으면 REVIEW.
5. 출력은 JSON만. reason은 최대 10단어.

출력 형식 (JSON only):
{"label":"IMPORTANT|REVIEW|FINANCE|DEV|SHOPPING|PROMOTION","action":"keep|archive","reason":"short reason"}

실행 규칙:
- IMPORTANT, REVIEW → action: keep (inbox 유지)
- FINANCE, DEV, SHOPPING, PROMOTION → action: archive"""


def get_service():
    """Gmail API 서비스 반환. 토큰 없으면 브라우저 OAuth."""
    from google.auth.transport.requests import Request
    from google_auth_oauthlib.flow import InstalledAppFlow
    from googleapiclient.discovery import build

    creds = None
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE, "rb") as f:
            creds = pickle.load(f)

    if creds and creds.expired and creds.refresh_token:
        try:
            creds.refresh(Request())
            with open(TOKEN_FILE, "wb") as f:
                pickle.dump(creds, f)
        except Exception:
            creds = None

    if not creds or not creds.valid:
        if not os.path.exists(SECRET_FILE):
            print(f"❌ client_secret.json 없음 — {SECRET_FILE}")
            return None
        flow = InstalledAppFlow.from_client_secrets_file(SECRET_FILE, SCOPES)
        creds = flow.run_local_server(port=0)
        with open(TOKEN_FILE, "wb") as f:
            pickle.dump(creds, f)
        print("✅ Gmail 인증 완료")

    return build("gmail", "v1", credentials=creds)


def _get_or_create_label(service, name: str) -> str:
    """라벨 ID 반환. 없으면 생성."""
    result = service.users().labels().list(userId="me").execute()
    for lbl in result.get("labels", []):
        if lbl["name"] == name:
            return lbl["id"]
    created = service.users().labels().create(
        userId="me",
        body={"name": name, "labelListVisibility": "labelShow", "messageListVisibility": "show"}
    ).execute()
    return created["id"]


def _build_label_map(service) -> dict[str, str]:
    return {name: _get_or_create_label(service, name) for name in LABEL_NAMES}


def _fetch_unprocessed(service, max_results: int = 50) -> list[dict]:
    """INBOX 에서 소미 라벨이 없는 메일 목록 반환."""
    exclude = " ".join(f"-label:{n}" for n in LABEL_NAMES)
    query = f"in:inbox {exclude}"
    resp = service.users().messages().list(
        userId="me", q=query, maxResults=max_results
    ).execute()
    return resp.get("messages", [])


def _get_email_data(service, msg_id: str) -> dict:
    """메일 헤더 + snippet 추출."""
    msg = service.users().messages().get(
        userId="me", id=msg_id, format="metadata",
        metadataHeaders=["From", "Subject"]
    ).execute()

    headers = {h["name"]: h["value"] for h in msg.get("payload", {}).get("headers", [])}
    has_attachment = any(
        p.get("filename") for p in msg.get("payload", {}).get("parts", [])
    )

    return {
        "id": msg_id,
        "from": headers.get("From", ""),
        "subject": headers.get("Subject", ""),
        "snippet": msg.get("snippet", ""),
        "has_attachment": has_attachment,
    }


def _classify(email: dict) -> dict:
    """LLM으로 메일 분류. 실패 시 REVIEW/keep 반환."""
    payload = {
        "from": email["from"],
        "subject": email["subject"],
        "snippet": email["snippet"],
        "has_attachment": email["has_attachment"],
    }
    user_msg = json.dumps(payload, ensure_ascii=False)
    raw = llm_text(user_msg, system=SYSTEM_PROMPT, lm_first=True, max_tokens=80)

    if not raw:
        return {"label": "REVIEW", "action": "keep", "reason": "LLM 응답 없음"}

    # JSON 파싱 — LLM이 마크다운 코드블록을 감쌀 수도 있음
    raw = raw.strip()
    if raw.startswith("```"):
        raw = raw.split("```")[1]
        if raw.startswith("json"):
            raw = raw[4:]
    try:
        result = json.loads(raw.strip())
        label = result.get("label", "REVIEW").upper()
        if label not in LABEL_NAMES:
            label = "REVIEW"
        action = "keep" if label in KEEP_IN_INBOX else "archive"
        return {"label": label, "action": action, "reason": result.get("reason", "")}
    except Exception:
        return {"label": "REVIEW", "action": "keep", "reason": "파싱 실패"}


def _apply(service, msg_id: str, label_id: str, action: str):
    """라벨 적용. archive면 INBOX 라벨 제거."""
    body: dict = {"addLabelIds": [label_id], "removeLabelIds": []}
    if action == "archive":
        body["removeLabelIds"] = ["INBOX"]
    service.users().messages().modify(userId="me", id=msg_id, body=body).execute()


def run(max_emails: int = 50, dry_run: bool = False) -> str:
    """메인 실행. 처리 요약 문자열 반환."""
    report(AGENT_NAME, "Gmail 정리 시작")
    service = get_service()
    if not service:
        return "❌ Gmail 인증 실패"

    messages = _fetch_unprocessed(service, max_results=max_emails)
    if not messages:
        return "📭 정리할 메일 없음"

    label_map = _build_label_map(service)
    counts: dict[str, int] = {n: 0 for n in LABEL_NAMES}
    archived = 0
    errors = 0

    for msg in messages:
        try:
            email = _get_email_data(service, msg["id"])
            result = _classify(email)
            label = result["label"]
            action = result["action"]
            label_id = label_map[label]

            if not dry_run:
                _apply(service, msg["id"], label_id, action)

            counts[label] += 1
            if action == "archive":
                archived += 1

            print(f"[{label}] {action:7} | {email['subject'][:50]}")
        except Exception as e:
            errors += 1
            print(f"❌ 처리 실패 ({msg['id']}): {e}")

    total = len(messages)
    kept = total - archived
    summary_lines = [f"📬 <b>[소미] Gmail 정리 완료</b> ({datetime.datetime.now().strftime('%H:%M')})"]
    summary_lines.append(f"총 {total}건 처리 | inbox 유지 {kept}건 | archive {archived}건")
    for label in LABEL_NAMES:
        if counts[label]:
            summary_lines.append(f"  {label}: {counts[label]}건")
    if errors:
        summary_lines.append(f"  ⚠️ 오류: {errors}건")
    if dry_run:
        summary_lines.append("  (dry-run — 실제 변경 없음)")

    return "\n".join(summary_lines)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="소미 — Gmail Inbox Zero 에이전트")
    parser.add_argument("--max", type=int, default=50, help="최대 처리 메일 수 (기본 50)")
    parser.add_argument("--dry-run", action="store_true", help="실제 변경 없이 분류만 확인")
    parser.add_argument("--no-notify", action="store_true", help="텔레그램 알림 비활성화")
    args = parser.parse_args()

    summary = run(max_emails=args.max, dry_run=args.dry_run)
    print("\n" + summary.replace("<b>", "").replace("</b>", ""))

    if not args.no_notify:
        send(summary)
