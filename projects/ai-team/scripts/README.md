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
| `fleet_bootstrap.py` | `git pull` 후 함대 자동 기동(post-merge 훅). 기계 전환은 `--setup` 한 번. 세 관문(BOTS_OFF·지정 운영기·playwright)을 통과할 때만 뜬다 — 이중 가동 방지가 본업. 절차: [HANDBOOK §4-5](../../../HANDBOOK.md) |

## Placement Rules

- Agent-specific tools belong in `projects/ai-team/skills/<agent>/tools/`.
- Shared Python helpers belong in `projects/ai-team/_shared/`.
- Generated reports, logs, caches, and media belong under `reports/`, `.logs/`,
  `output/`, or ignored runtime-state paths.
