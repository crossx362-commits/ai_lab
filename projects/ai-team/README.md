# AI Team - Multi-Agent Trading System

**Status**: ✅ Production Ready (마이그레이션 완료)  
**Last Updated**: 2026-06-18

---

## 🚀 Quick Start

### 1. Install Dependencies
```bash
pip install pyupbit requests cryptography google-generativeai
```

### 2. Configure Environment
```bash
# 환경변수 설정
cp .env.example .env
nano .env

# 암호화 (선택사항)
python _shared/env.py encrypt .env .env.encrypted
```

### 3. Run Trading Bots
```bash
# 데이브 (보수적)
python skills/데이브_주식/tools/upbit_auto_trader.py --daemon

# 레오 (공격적)
python skills/레오_트레이더/tools/leo_aggressive_trader.py --daemon

# 현빈 (시장 인텔)
python skills/현빈_전략가/tools/crypto_market_intelligence.py --daemon

# 영숙 (텔레그램 봇)
python skills/영숙_비서/tools/telegram_receiver.py
```

### 4. Check Status
```bash
# 하네스로 시스템 상태 확인
python harness/check_all.py

# 보유 현황 확인
python scripts/check_holdings.py
```

---

## 📁 Structure

```
projects/ai-team/
├── _shared/                  # 통합 모듈 (5 files, 667 lines)
│   ├── env.py               # 환경변수 통합
│   ├── llm.py               # LLM 클라이언트 (Ollama → GPT → Gemini)
│   ├── notify.py            # Telegram + 에이전트 상태
│   ├── process.py           # ProcessLock + DuplicateGuard
│   └── utils.py             # 유틸리티
├── harness/                 # 시스템 검증
│   ├── check_all.py         # 전체 상태 체크
│   └── README.md
├── scripts/                 # 운영 스크립트
│   ├── check_holdings.py
│   ├── daily_balance_check.py
│   └── start_trading_team.py
├── skills/                  # 에이전트별 도구
│   ├── 데이브_주식/
│   ├── 레오_트레이더/
│   ├── 현빈_전략가/
│   ├── 영숙_비서/
│   ├── 예원_CEO/
│   └── ...
└── docs/                    # 문서
```

---

## 🤖 Agents

| Agent | Role | Status |
|-------|------|--------|
| **데이브** | 보수적 매매 (BTC, ETH, SOL, XRP) | ✅ Active |
| **레오** | 공격적 단타 (DOGE, PEPE, NEAR, SUI) | ✅ Active |
| **현빈** | 시장 인텔 수집 (Fear & Greed, 김프) | ✅ Active |
| **영숙** | Telegram 봇 + 스케줄러 | ✅ Active |
| **예원** | CEO - Task dispatcher | 🟡 Manual |
| **케빈** | Vercel + Supabase 인프라 | 🟡 Manual |
| **티모** | UI/UX 리뷰 | 🟡 Manual |
| **코다리** | 개발자 - Health checks | 🟡 Manual |

---

## 📚 Documentation

- **[MIGRATION_COMPLETE.md](MIGRATION_COMPLETE.md)** - 마이그레이션 최종 요약
- **[MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)** - 마이그레이션 가이드
- **[CONSOLIDATION_SUMMARY.md](CONSOLIDATION_SUMMARY.md)** - 프로젝트 개요
- **[harness/README.md](harness/README.md)** - 하네스 사용법
- **[../../CLAUDE.md](../../CLAUDE.md)** - 전체 시스템 가이드

---

## 🔧 Maintenance

### Health Check
```bash
python harness/check_all.py
```

### Restart Bots
```bash
# 개별 재시작
pkill -f upbit_auto_trader.py
python skills/데이브_주식/tools/upbit_auto_trader.py --daemon

# 전체 재시작
bash scripts/launchd/uninstall.sh  # macOS
bash scripts/launchd/install.sh
```

### View Logs
```bash
# 트레이딩 로그
tail -f output/trading_logs/dave_*.log

# 영숙 봇 로그
tail -f skills/영숙_비서/tools/telegram_receiver.log
```

---

## 🚨 Troubleshooting

### Bot Not Running
```bash
# 프로세스 확인
ps aux | grep python

# 락 파일 확인 (Windows)
# Named Mutex 자동 해제됨

# 수동 재시작
python skills/데이브_주식/tools/upbit_auto_trader.py --once
```

### Environment Variables Missing
```bash
# .env 복호화
python _shared/env.py decrypt .env.encrypted .env.decrypted

# 환경변수 검증
python scripts/agents/check_agent_env_connections.py
```

### Import Errors
```bash
# 통합 모듈 확인
ls -la _shared/{env,llm,notify,process,utils}.py

# 하네스로 구조 검증
python harness/check_all.py
```

---

## 📊 Migration Stats

**Completed**: 2026-06-18  
**Files Changed**: 53  
**Modules Consolidated**: 24 → 5 (**82% reduction**)  
**Token Savings**: 3,718 → 667 lines  

---

## 🏆 Credits

- **Architecture**: Unified module system
- **Migration**: Automated batch conversion (53 files)
- **Testing**: Harness-based validation
- **Documentation**: 6 comprehensive guides

---

**For detailed setup and usage, see [CLAUDE.md](../../CLAUDE.md)**
