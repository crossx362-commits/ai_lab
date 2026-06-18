# Repository Classification

Last reviewed: 2026-06-17

This file classifies the active Markdown files, scripts, and bots in `D:\ai_lab`. It is intentionally an operating index, not a replacement for `PROJECT_OVERVIEW.md`.

## Source Areas

| Category | Keep In | Notes |
| --- | --- | --- |
| Root operating docs | `AGENTS.md`, `PROJECT_OVERVIEW.md`, `README.md`, `CLAUDE.md`, `SKILL.md` | Root docs define workspace behavior and broad overview. `README.md` is now the lightweight entry point. |
| Setup docs | `docs/setup/` | Environment, Notion, Instagram, security, and automation setup guides. |
| Planning docs | `docs/plans/` | Current migration and cleanup plans that should be executed through the harness. |
| Reports | `reports/` | Generated research, history, meetings, and inspection reports. Keep current reports; archive exact duplicates can be removed. |
| Petnna docs | `projects/petnna/*.md`, `projects/petnna/docs/` | Product docs, policies, changelog, Supabase setup. |
| AI Team docs | `projects/ai-team/*.md`, `projects/ai-team/docs/`, `projects/ai-team/scripts/README.md`, `projects/ai-team/skills/README.md` | Project-level and agent-framework documentation. |
| AI Team harness | `projects/ai-team/harness/` | Read-only runtime and structure checks. Run before and after folder cleanup or migration. |
| Agent skills | `projects/ai-team/skills/<agent>/SKILL.md`, `knowledge/*.md` | Agent identity, behavior, and learned knowledge. Do not merge across agents unless the agent roster changes. |
| Shared skill docs | `projects/ai-team/skills/공용스킬/` | Cross-agent reusable guidance. |
| Packs and templates | `connect-ai-packs/`, `projects/ai-team/assets/brain-seeds/` | Reusable templates and pack examples. Treat as library assets. |

## Script And Bot Categories

| Category | Primary Files | Merge / Cleanup Rule |
| --- | --- | --- |
| Trading launchers | `projects/ai-team/scripts/start_trading_team.py`, `run_trading_team_background.py`, `run_trader_daemon.py`, root `start_trading.bat`, `restart_trading.bat` | Keep Python scripts as canonical. Root batch files are Windows convenience wrappers only. |
| Trading bots | `skills/데이브_주식/tools/upbit_auto_trader.py`, `skills/레오_트레이더/tools/leo_aggressive_trader.py`, `skills/현빈_전략가/tools/crypto_market_intelligence.py` | Do not merge: different risk profiles and daemon cadences. |
| Trading utilities | `check_holdings.py`, `daily_balance_check.py`, `daily_trading_learning.py`, `upbit_public.py`, `upbit_analyzer.py` | Keep as support tools. Move new shared exchange logic into `_shared/` only after cross-agent testing. |
| Telegram bot | `skills/영숙_비서/tools/telegram_receiver.py`, `start_telegram_bot.ps1`, `scripts/start_youngsuk_bot.cmd`, `run_youngsuk_daemon.py` | `telegram_receiver.py` is canonical; wrappers only start and supervise it. |
| Scheduling and reports | `calendar_manager.py`, `posting_scheduler.py`, `reports_manager.py`, `schedule_manager.py`, `start_daily_automation.py` | Keep separated by workflow; only consolidate if duplicated function bodies appear. |
| Orchestration | `예원_CEO/tools/yewon_dispatcher.py`, `upload_manager.py`, `skill_auditor.py`, `evaluate_feedback.py` | Keep under 예원 because these control cross-agent routing. |
| Infra | `케빈_인프라/tools/vercel_manager.py`, `sync_env_to_vercel.py`, `supabase_manager.py`, `petnna_monitor.py` | Keep under 케빈. These can change live cloud resources. |
| Developer health/tools | `코다리_개발자/tools/web_preview.py`, `web_init.py`, `agent_health_check.py`, `ollama_health_check.py`, `lint_test.py` | Keep under 코다리. |
| Security/investigation | `경수_수사관/tools/comment_forensics.py`, `content_inspector.py`, `approval_kyungsoo.py` | Keep under 경수. |
| Legal/tax | `로율_변호사/tools/tax_simulator.py` | Keep under 로율. |
| Petnna frontend | `projects/petnna/js/*.js`, `projects/petnna/js/templates/*.js`, `projects/petnna/api/*.js`, `projects/petnna/sw.js` | App source. Do not merge without browser testing. |
| Extension source | `projects/ai-team/src/*.ts` | VS Code extension source. Do not mix with runtime bot scripts. |

## Consolidation Decisions

| Decision | Result |
| --- | --- |
| Root README vs project overview | `README.md` is the concise entry point; `PROJECT_OVERVIEW.md` remains the full overview. |
| Script list | `projects/ai-team/scripts/README.md` remains the canonical operations script index. |
| Telegram docs | `docs/TELEGRAM_BOT_README.md` remains the canonical Youngsuk bot guide. |
| Exact duplicate inspection archive | The duplicate archived report can be removed when identical to the current inspection report. |
| `.backups` plaintext env copies | Remove. These are obsolete and risky because the active secrets policy uses central `.env` plus encrypted copies. |
| `.archive` old workspace backup | Remove from active workspace if no manual restore is needed. Git ignores it and it duplicates old source state. |
| `__pycache__` | Remove. Python regenerates it. |
| `node_modules` | Leave unless a clean dependency reinstall is planned. Network is restricted, so deleting it would make local TS/Node tooling harder to run. |
| `output` | Leave. Some files are tracked generated deliverables; clean only by explicit artifact policy. |
| `projects/ai-team/reports/` | Keep empty. Active generated reports belong under root `reports/`; runtime logs belong under `output/`. |
| Runtime state files | Git-ignored: Telegram send cache, Youngsuk `last_run.json`, harness latest snapshot, and live Hyunbin research/market JSON. These are regenerated by running agents. |

## Cleanup Policy Going Forward

- Put new reusable Python helpers in `projects/ai-team/_shared/`.
- Put agent-specific tools in `projects/ai-team/skills/<agent>/tools/`.
- Put one-off diagnostics in `projects/ai-team/scripts/agents/`.
- Put generated reports under `reports/` and large generated media under `output/`.
- Avoid new root scripts unless they are Windows convenience wrappers for a canonical script under `projects/ai-team/scripts/`.
- Run `python projects/ai-team/harness/check_all.py` before and after any structure change.
- Do not move live runtime paths until their producers and consumers are migrated together.
