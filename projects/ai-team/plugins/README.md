# ai_lab 한국시장 금융 플러그인 마켓플레이스

Anthropic [financial-services-plugins](https://github.com/anthropics/financial-services-plugins)를
**한국시장(KRX·KIS API)·한국어·ai_lab 3 에이전트(소미·예원·영숙)**에 맞게 커스터마이징한
Claude Code 플러그인 마켓플레이스.

원본은 미국 기관투자(CapIQ·FactSet·DCF/LBO·KYC 등) 18개 플러그인이지만, 우리 운영과 무관한 것은
제외하고 **우리 3 에이전트에 매핑되는 것만** 한국시장 버전으로 이식했다.

## 구성

| 플러그인 | 에이전트 | 원본 | 핵심 |
|---------|---------|------|------|
| `somi-kr-equity-research` | 소미(분석가) | equity-research + market-researcher + earnings-reviewer | 장전브리핑·종목발굴·섹터분석·실적분석·투자논리추적 |
| `yewon-research-dispatcher` | 예원(CEO) | market-researcher (agent) | 리서치 요청을 소미 스킬로 분해·지휘하는 오케스트레이터 |
| `youngsuk-briefing` | 영숙(비서) | meeting-prep-agent | 데일리 브리핑·리포트 작성 (텔레그램 요약 + 노션 풀) |

### 소미 스킬 (`/장전브리핑` `/종목발굴` `/섹터분석` `/논리점검`)
- **morning-brief** — 08:30 장전 브리핑 (← morning-note)
- **idea-screen** — 거래대금 상위를 소미점수·수급·기대값으로 채점 (← idea-generation)
- **sector-overview** — 한국 섹터/테마 분석 (← sector-overview)
- **earnings-analysis** — 영업이익 서프라이즈 중심 실적 분석 (← earnings-analysis)
- **thesis-tracker** — 기대값 엔진 연계 투자논리·청산 추적 (← thesis-tracker)

### 한국화 핵심 변경점
- 미국 7am 모닝미팅 → **한국 08:30 장전**, KST·₩원·코스피/코스닥
- CapIQ/FactSet MCP → **KIS API + 소미 Python 도구**(`somi_kis_reporter`, `somi_signal_engine`, `somi_screener`, `short_covering_analyzer`, `market_regime`)
- EPS 중심 → **영업이익·수급(외인/기관)·소미 점수·기대값** 중심
- 출력: **텔레그램 요약 + 노션 풀리포트** 분리 규칙
- 사용자 규칙 반영: 노하드코딩 / 모의=자동·실거래=승인 / 주식질문→분석+텔레그램 / 성장 도크트린

## 설치 (등록)

이 마켓플레이스를 Claude Code에 등록한다(인터랙티브 세션에서):

```
/plugin marketplace add d:/ai_lab/projects/ai-team/plugins
/plugin install somi-kr-equity-research@ai-lab-korea-finance
/plugin install yewon-research-dispatcher@ai-lab-korea-finance
/plugin install youngsuk-briefing@ai-lab-korea-finance
```

> 마켓플레이스 이름은 `.claude-plugin/marketplace.json`의 `ai-lab-korea-finance`.

설치 후 사용 예:
- `/장전브리핑` → 소미 장전 브리핑
- `/종목발굴 코스닥 5` → 코스닥 톱5 발굴
- `/섹터분석 반도체` → 반도체 섹터 리포트
- `/논리점검` → 보유 포지션 전체 투자논리 점검
- `/데일리브리핑` → 영숙 일일 브리핑
- "반도체 테마로 리서치 돌려줘" → 예원 `research-dispatcher` 에이전트가 소미 스킬을 지휘

## 주의
- 스킬은 **방법론(판단 레이어)**이고, 데이터·체결은 기존 소미 Python 도구가 담당한다. 둘은 분리.
- 실거래 체결은 항상 승인 게이트를 거친다([[paper-mode-autotrade]]).
- 디렉터리/플러그인 `name`은 ASCII 슬러그(로더 호환), 사람이 보는 텍스트·트리거·내용은 전부 한국어.
