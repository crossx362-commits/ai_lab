---
name: youngsuk
description: 사장님 전담 AI 비서. 텔레그램 실시간 응답, 에이전트 현황 조회, 코인 거래 현황, Gmail 정리(소미), 구글 캘린더 연동 담당.
---

# 영숙 — AI 비서 스킬

## 역할

사장님의 텔레그램 메시지에 응답하는 메인 봇.
메시지를 LLM으로 분석해 필요한 에이전트·도구를 호출하고 자연어로 답한다.

## 실행 방식

macOS launchd로 상시 실행 (`com.ailab.youngsuk`).

```bash
# 재시작
launchctl kickstart -k gui/$(id -u)/com.ailab.youngsuk

# 상태 확인
launchctl list | grep com.ailab.youngsuk
```

로그: `output/bot_logs/youngsuk.out.log`

## 메시지 라우팅

LLM이 메시지 의도를 분석해 액션을 결정한다.

| 의도 | 액션 | 예시 |
|------|------|------|
| `coin` | 코인 보유 현황 조회 (업비트 API) | "코인 현황", "수익 얼마야" |
| `agent` | 에이전트 실행 상태 조회 | "다들 뭐해", "에이전트 현황" |
| `stock` | 한국 주식 시세 조회 (KIS API) | "삼성전자 주가" |
| `mail` | 소미 Gmail 정리 실행 | "메일 정리해", "받은편지함 처리" |
| `search` | 인터넷 검색 | "최신 뉴스 찾아봐" |
| `chat` | 일반 대화 | "안녕", 투자 질문 등 |

## 주요 도구 파일

| 파일 | 역할 |
|------|------|
| `telegram_receiver.py` | 메인 봇 (폴링 + 응답) |
| `schedule_manager.py` | 스케줄 로드/저장 |
| `calendar_manager.py` | Google Calendar 연동 |
| `posting_scheduler.py` | 포스팅 일정 관리 |
| `upload_approval_flow.py` | 업로드 승인 플로우 |
| `youtube_recommender.py` | 유튜브 추천 |
| `reports_manager.py` | 리포트 정리 |
| `schedules.json` | 활성 스케줄 목록 |

## 지원 조회 종목 (주식)

삼성전자, SK하이닉스, 우리기술

## 에이전트 연동

| 에이전트 | 연동 방식 |
|----------|-----------|
| 소미 (Gmail) | `importlib`으로 `gmail_manager.run()` 직접 호출 |
| 데이브/레오/시그널 | `pgrep`으로 실행 상태 확인, 업비트 API로 보유 조회 |

## 페르소나 (SYSTEM 프롬프트 요약)

- 톡톡 튀고 솔직한 비서. 모르면 모른다고 한다.
- 정확성 우선. 확인 안 된 수치는 말하지 않는다.
- 사장님은 개발자/서비스 운영자. 이론보다 실행 방법 우선 답변.
- 불필요한 사과·자기소개·장황한 설명 금지.
