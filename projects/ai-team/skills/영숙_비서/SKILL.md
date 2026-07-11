---
name: youngsuk
description: 텔레그램 게이트웨이 비서 — 폴링 흐름만 소유하고, 도메인 툴은 주인 에이전트(예원) 모듈의 BOT_TOOLS를 병합해 클로드 tool use로 디스패치한다.
---

# 영숙 비서 (게이트웨이)

영숙은 사장님의 텔레그램 메시지에 응답하는 메인 비서이자 **게이트웨이**입니다(2026-07-04 개편). 봇 런타임(폴링·핸들러)만 직접 소유하고, 도메인 기능은 주인 에이전트 모듈에서 수집해 위임합니다. 함수호출 LLM은 **클로드 tool use**(2026-07-05 GPT 퇴출, `ANTHROPIC_BOT_MODEL` 기본 haiku). (2026-07-08 주식·코인 도메인 전면 삭제로 소미·한별 위임 경로 제거.)

## Responsibilities

- 텔레그램 메시지 수신·응답 (발신자 잠금 fail-closed)
- 일정 조회·저장, 데몬 시작/중지 보조
- 도메인 요청 위임: 오케스트레이션→예원 툴, 펫나 QA/개발 현황→봄이·수리 등

## Main Tools

| File | Purpose |
| --- | --- |
| `telegram_receiver.py` | 게이트웨이: 폴링·라우팅 + BOT_TOOLS 병합(클로드 tool use) |
| `bot_common.py` | 공유 헬퍼 (게이트웨이·툴 모듈 공용) |
| `bot_tools_info.py` | 영숙 본연 툴: 날씨·일정 (BOT_TOOLS export) |
| `../예원_CEO/tools/yewon_bot_tools.py` | 오케스트레이션 툴 (예원 소유, 게이트웨이가 병합) |
| `agent_controller.py` | 데몬 시작/중지/상태 |
| `schedule_manager.py` / `schedules.json` | 정시 잡 SSOT(launchd `schedule_sync`가 materialize) |

**새 봇 기능 추가 = 주인 에이전트 모듈의 BOT_TOOLS에 엔트리 1개** (구 '4곳 등록' 소멸 — 게이트웨이는 손대지 않음).

## Routing Notes

- 오케스트레이션·시장종합·브리핑·하네스·스킬 감사는 예원 담당입니다.
- 펫나 QA/개발/디자인/기획/백엔드/테스트 현황은 봄이·수리·미오·나무·백호·테오 담당입니다.
- 삭제된 과거 에이전트 이름을 받으면 직접 실행하지 않고 현재 담당 에이전트로 안내합니다.

## Run

맥에서는 launchd 상시 데몬(`com.ailab.youngsuk`, KeepAlive)이 `youngsuk_launcher.sh`를 통해 기동한다.
수동 실행/재기동은 `agent_controller.py`로:

```bash
python3 projects/ai-team/skills/영숙_비서/tools/agent_controller.py 영숙 restart
```
