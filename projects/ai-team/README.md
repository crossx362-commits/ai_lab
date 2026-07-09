# AI Team

**Status**: ✅ Production (예원·영숙 + 펫나 자동개발팀 6명)
**Last Updated**: 2026-07-09

---

## 🚀 Quick Start

### 1. Configure Environment
```bash
python _shared/env.py encrypt .env .env.encrypted
```

### 2. Run Core Daemons
```bash
# 영숙 (텔레그램 봇)
python skills/영숙_비서/tools/telegram_receiver.py

# 예원 (하네스 워치독)
python skills/예원_CEO/tools/harness_monitor.py
```

또는 개별 에이전트 제어:
```bash
python skills/영숙_비서/tools/agent_controller.py <에이전트명> start|stop|restart|status
```

### 3. Check Status
```bash
python harness/check_all.py
```

---

## 📁 Structure

```
projects/ai-team/
├── _shared/                  # 공통 모듈
│   ├── env.py                # 환경변수 통합
│   ├── llm.py                # LLM 클라이언트 (Ollama → 구독 CLI → Gemini)
│   ├── telegram.py           # 텔레그램 발신 (전 에이전트 공용, 유일 경로)
│   ├── notify.py             # 데몬 프로세스 상태 조회
│   ├── process.py            # ProcessLock + DuplicateGuard
│   └── utils.py              # 유틸리티
├── harness/                  # 시스템 검증
│   ├── check_all.py          # 전체 상태 체크
│   └── README.md
├── skills/                   # 에이전트별 도구
│   ├── 예원_CEO/
│   ├── 영숙_비서/
│   ├── 봄이_QA/
│   ├── 수리_개발자/
│   ├── 테오_테스트/
│   ├── 백호_백엔드/
│   ├── 미오_디자인/
│   └── 나무_기획/
└── docs/                     # 문서
```

---

## 🤖 Agents

| Agent | Role | Status |
|-------|------|--------|
| **예원** | CEO — 오케스트레이션·하네스·워치독 | ✅ Active |
| **영숙** | 텔레그램 게이트웨이·일정·정시 잡 | ✅ Active |
| **봄이** | 펫나 QA 상시 순찰 | ✅ Active |
| **수리** | 펫나 자동 개선 엔진 | ✅ Active |
| **테오** | 펫나 E2E 테스트 자동화 | ✅ Active |
| **백호** | Supabase 계약 감사 | ✅ Active |
| **미오** | 펫나 디자인 리뷰 | ✅ Active |
| **나무** | 펫나 기획 PM | ✅ Active |

> 과거 주식·코인 트레이딩 에이전트(소미·한별·행크·유나·레온·마켓데스크·지아)와 그 이전 세대
> 에이전트(데이브·레오·시그널·펄스·케빈·경수·코다리·티모·로율)는 전부 삭제됨 —
> git 이력에서 복구 가능.

---

## 📚 Documentation

- **[harness/README.md](harness/README.md)** — 하네스 사용법
- **[../../CLAUDE.md](../../CLAUDE.md)** — 전체 시스템 가이드

---

## 🔧 Maintenance

### Health Check
```bash
python harness/check_all.py
```

### Restart a Daemon
```bash
python skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

### View Logs
```bash
tail -f output/bot_logs/youngsuk_telegram.out.log
```

---

## 🚨 Troubleshooting

### Bot Not Running
```bash
python skills/영숙_비서/tools/agent_controller.py 영숙 status
python skills/영숙_비서/tools/agent_controller.py 영숙 restart
```

### Environment Variables Missing
```bash
python _shared/env.py decrypt .env.encrypted .env.decrypted
```

### Import Errors
```bash
ls -la _shared/{env,llm,telegram,notify,process,utils}.py
python harness/check_all.py
```

---

**For detailed setup and usage, see [CLAUDE.md](../../CLAUDE.md)**
