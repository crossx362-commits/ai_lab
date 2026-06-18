# AI Team Harness Migration Plan

Last updated: 2026-06-18

## Goal

Use `projects/ai-team/harness/check_all.py` as the gate for repo cleanup and folder migration.

## Current Safe Boundary

Do not move these live paths yet:

- `output/trading_logs/*`
- `output/bot_logs/*`

They are read or written by currently running agents.

Moved to root `reports/research/`:

- `crypto_market_intel.json`
- `hyunbin_alert_state.json`

Removed from `projects/ai-team/reports/`:

- `pids/dave.lock` (stale Windows runtime file; active locks use Windows Named Mutex)

## Cleanup Order

1. Run `python projects/ai-team/harness/check_all.py`.
2. Move only documentation or archived reports that are not read by runtime scripts.
3. Run the harness again.
4. Search for old paths with `rg`.
5. Update `docs/REPOSITORY_CLASSIFICATION.md`.
6. Commit the smallest coherent change.

## Migration Rule

Any live runtime file move must update producers, consumers, docs, and harness checks in the same change.
