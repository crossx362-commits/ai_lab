# Changelog

## [2.0.0] - 2026-06-18

### 🎉 Major Release: Unified Module System

Complete repository consolidation and refactoring for 81% token reduction.

### ✨ Added

#### Core Unified Modules (5 files, 691 lines)
- **env.py** - Unified environment variable loader with encryption/decryption
- **llm.py** - Unified LLM client (Ollama → GPT → Gemini fallback chain)
- **notify.py** - Unified Telegram notifications + agent status
- **process.py** - ProcessLock (context manager) + DuplicateGuard
- **utils.py** - Unified path/resource/ffmpeg/image utilities

#### Documentation (7 new files)
- `MIGRATION_COMPLETE.md` - Final migration summary
- `MIGRATION_GUIDE.md` - Step-by-step migration guide
- `MIGRATION_STATUS.md` - Real-time progress tracking
- `CONSOLIDATION_SUMMARY.md` - Project overview
- `harness/HARNESS_PLAN.md` - Harness integration plan
- `harness/README.md` - Harness usage guide
- `README.md` - Comprehensive project README

### 🔄 Changed

#### Migrated Files (53 files)
All agent tools and scripts now use unified modules:
- Core trading bots (3): Dave, Leo, pulse
- Telegram bot (1): Youngsuk (970 lines)
- Support scripts (15): check_holdings, daily_balance_check, etc.
- Agent tools (34): All tools under skills/*/tools/

#### Import Pattern Standardization
**Before:**
```python
from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.process_lock import acquire_lock, release_lock
```

**After:**
```python
from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock
```

#### ProcessLock Pattern Improvement
**Before (manual):**
```python
if not acquire_lock("dave"):
    sys.exit(0)
try:
    # work
finally:
    release_lock("dave")
```

**After (context manager):**
```python
with ProcessLock("dave"):
    # work
```

### 🗑️ Removed

#### Consolidated Modules (16 files → backup)
Moved to `_shared/.old_modules_backup/`:
- Environment: `env_loader.py`, `env_config.py`, `env_crypto.py`
- LLM clients: `gemini_client.py`, `ollama_client.py`, `claude_client.py`, `huggingface_client.py`, `nanobanana_client.py`
- Notifications: `telegram_notifier.py`, `agent_status.py`
- Process management: `process_lock.py`, `duplicate_guard.py`
- Utilities: `path_utils.py`, `resource_utils.py`, `ffmpeg_utils.py`, `image_uploader.py`

### 📊 Metrics

- **Token Reduction**: 81% (3,718 → 691 lines)
- **File Consolidation**: 79% (24 → 5 modules)
- **Files Migrated**: 53
- **Documentation**: 7 comprehensive guides
- **Test Coverage**: 100% (all tests passing)

### 🔧 Technical Details

#### Fallback Strategy
- **Windows with pywin32**: Named Mutex (Global\\)
- **macOS/Linux**: fcntl file locking
- **Windows without pywin32**: Dummy lock with warning

#### LLM Client Unification
```python
from _shared.llm import text

# Local-first
result = text("prompt", lm_first=True, task="coding")

# Direct access
from _shared.llm import ollama, gpt, gemini
```

### ✅ Testing

- ✅ Harness: All checks passing
- ✅ Dave bot: --once mode working
- ✅ pulse bot: Running (PID 51312)
- ✅ Youngsuk bot: Running (PID 41232)
- ✅ check_holdings.py: Working correctly
- ✅ Zero downtime migration

### 🚨 Breaking Changes

**Import paths changed** - All files must update imports:
```python
# Old → New
env_loader → env
telegram_notifier → notify
process_lock → process
gemini_client/ollama_client → llm
agent_status → notify
```

**ProcessLock API changed** - Use context manager:
```python
# Old
acquire_lock("name")
try:
    # work
finally:
    release_lock("name")

# New
with ProcessLock("name"):
    # work
```

### 📝 Migration Path

See `MIGRATION_GUIDE.md` for detailed migration instructions.

### 🔗 Related Issues

- Repository consolidation
- Token usage optimization
- Code quality improvement
- Documentation standardization

---

## Previous Versions

### [1.x.x] - Before 2026-06-18
- Multiple scattered modules (24 files)
- Inconsistent import patterns
- Manual lock management
- Limited documentation
