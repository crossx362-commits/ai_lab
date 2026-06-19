# Harness Simplification Plan

## Goal
Reduce token usage by 70% through aggressive consolidation while maintaining all runtime functionality.

## Phase 1: Consolidate _shared (24 → 5 files)

### Keep (Core Runtime)
1. **env.py** ← merge: `env_loader.py`, `env_config.py`, `env_crypto.py`
2. **llm.py** ← merge: `gemini_client.py`, `ollama_client.py`, `claude_client.py`, `huggingface_client.py`, `nanobanana_client.py`
3. **notify.py** ← merge: `telegram_notifier.py`, `agent_status.py`
4. **process.py** ← merge: `process_lock.py`, `duplicate_guard.py`
5. **utils.py** ← merge: `path_utils.py`, `resource_utils.py`, `ffmpeg_utils.py`, `image_uploader.py`

### Remove (Rarely Used)
- `calendar_client.py` → inline into Youngsuk's calendar_manager.py
- `notion_client.py` → inline into notion_report_manager.py
- `content_validator.py` → inline into upload_manager.py
- `knowledge_base.py` → merge into agent_council.py
- `history_recorder.py` → unused, delete

## Phase 2: Simplify Harness

### Single Check Script
`harness/check_all.py`:
- ✅ Env validation
- ✅ Runtime process check
- ✅ File structure validation
- ✅ Import health check
- ✅ API connectivity test

### Remove
- Separate health check scripts (already in `check_all.py`)

## Phase 3: Environment Variables

### Single Source of Truth
- Use `D:\ai_lab\.env` ONLY
- Remove all project-specific env loading logic
- Standardize import:
  ```python
  from _shared.env import load_env
  load_env()  # always loads root .env
  ```

## Phase 4: Skill Consolidation

### Trading
- Keep: `upbit_auto_trader.py` (Dave), `leo_aggressive_trader.py` (Leo), `market_signal.py` (Signal), `market_pulse.py` (Pulse)
- Remove: `upbit_public.py` (merge into upbit_analyzer.py), duplicate analyzers

### Telegram
- Keep: `telegram_receiver.py` (canonical bot)
- Remove: all wrapper scripts → use harness to launch

### Health Checks
- Keep: `agent_health_check.py` (comprehensive)
- Remove: `ollama_health_check.py`, `telegram_health_check.py` (merge into agent_health_check.py)

## Phase 5: Script Cleanup

### Keep (Operations)
- `start_trading_team.py` (canonical launcher)
- `check_holdings.py`
- `daily_balance_check.py`
- `start_daily_automation.py`

### Remove (Redundant Wrappers)
- Root `.bat` files
- `run_*.py` daemon wrappers → use launchd on macOS, Task Scheduler on Windows
- `cleanup_duplicate_processes.py` → harness handles this

## Migration Steps

1. ✅ Read REPOSITORY_CLASSIFICATION
2. Create consolidated _shared modules
3. Update all imports to use new modules
4. Test runtime (Youngsuk, Dave, Leo, pulse)
5. Remove old _shared files
6. Update CLAUDE.md
7. Run `harness/check_all.py`

## Success Criteria

- [ ] _shared: 24 files → 5 files
- [ ] Scripts: ~80 files → ~30 files
- [ ] All 4 daemons run without errors
- [ ] `check_all.py` shows all green
- [ ] Token usage reduced by 70% in agent imports
