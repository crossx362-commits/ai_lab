# AI Team Scripts

This folder contains operational scripts shared across agents or used to
inspect the ai-team runtime.

## Before Running

- Scripts can touch live services or Telegram messages. Prefer dry-run or
  diagnostic scripts first when available.
- Use the harness before and after structure changes:

```bash
python projects/ai-team/harness/check_all.py
```

## Main Scripts

| File | Purpose |
| --- | --- |
| `cleanup_duplicate_processes.py` | Cleans duplicate Python daemon processes. |

## Placement Rules

- Agent-specific tools belong in `projects/ai-team/skills/<agent>/tools/`.
- Shared Python helpers belong in `projects/ai-team/_shared/`.
- Generated reports, logs, caches, and media belong under `reports/`, `.logs/`,
  `output/`, or ignored runtime-state paths.
