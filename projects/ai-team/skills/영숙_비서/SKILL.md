---
name: youngsuk
description: 텔레그램 게이트웨이 비서 — 폴링·주문/신호 승인 흐름만 소유하고, 도메인 툴은 주인 에이전트(소미·한별·예원) 모듈의 BOT_TOOLS를 병합해 클로드 tool use로 디스패치한다.
---

# 영숙 비서 (게이트웨이)

영숙은 사장님의 텔레그램 메시지에 응답하는 메인 비서이자 **게이트웨이**입니다(2026-07-04 개편). 봇 런타임(폴링·핸들러·주문/신호 승인)만 직접 소유하고, 도메인 기능은 주인 에이전트 모듈에서 수집해 위임합니다. 함수호출 LLM은 **클로드 tool use**(2026-07-05 GPT 퇴출, `ANTHROPIC_BOT_MODEL` 기본 haiku).

## Responsibilities

- 텔레그램 메시지 수신·응답 (발신자 잠금 fail-closed)
- 수동 주문·소미제안 승인·매수신호 원터치 승인 (봇 상태와 강결합 — 게이트웨이 소유)
- 거래 모드(모의/실거래) 전환·조회
- 일정 조회·저장, 데몬 시작/중지 보조
- 도메인 요청 위임: 종목/투자→소미·한별 툴, 오케스트레이션→예원 툴

## Main Tools

| File | Purpose |
| --- | --- |
| `telegram_receiver.py` | 게이트웨이: 폴링·라우팅·주문/신호 승인 + BOT_TOOLS 병합(클로드 tool use) |
| `bot_common.py` | 공유 헬퍼: 종목 해석·의도 판별·서브프로세스 (게이트웨이·툴 모듈 공용) |
| `bot_tools_info.py` | 영숙 본연 툴: 날씨·일정 (BOT_TOOLS export) |
| `../소미_분석가/tools/somi_bot_tools.py` | 종목/투자 툴 13종 (소미·한별 소유, 게이트웨이가 병합) |
| `../예원_CEO/tools/yewon_bot_tools.py` | 오케스트레이션 툴 (예원 소유, 게이트웨이가 병합) |
| `agent_controller.py` | 데몬 시작/중지/상태 |
| `schedule_manager.py` / `schedules.json` | 정시 잡 SSOT(launchd `schedule_sync`가 materialize) |

**새 봇 기능 추가 = 주인 에이전트 모듈의 BOT_TOOLS에 엔트리 1개** (구 '4곳 등록' 소멸 — 게이트웨이는 손대지 않음).

## Routing Notes

- 실시간 감시·수급분석은 소미, 매수판단·발굴·튜닝은 한별 담당입니다.
- 시장종합·브리핑·하네스·스킬 감사는 예원 담당입니다.
- 삭제된 과거 에이전트 이름을 받으면 직접 실행하지 않고 현재 담당 에이전트로 안내합니다.

## Run

```powershell
powershell -ExecutionPolicy Bypass .\projects\ai-team\skills\영숙_비서\tools\start_telegram_bot.ps1
```
