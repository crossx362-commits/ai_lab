# ai-team Harness

Lightweight checks for the current ai-team runtime and repo layout.

Run from repo root:

```powershell
python projects/ai-team/harness/check_all.py
```

Checks:

- central env loading
- Youngsuk, Hyunbin, Dave, Leo process presence
- Youngsuk schedules and last-run file
- trading intelligence/log freshness
- core folder structure
- report layout drift

It also writes the latest status snapshot to:

```text
reports/status/harness_latest.json
```

Use it before and after folder cleanup or migration. The checks avoid secrets and do not print key values.

`projects/ai-team/reports/` is only allowed for live runtime exceptions that are still read by agents, currently PID locks. General generated reports should live under root `reports/`.
