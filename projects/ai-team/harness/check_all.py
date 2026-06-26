#!/usr/bin/env python3
"""Lightweight repo/runtime harness for ai-team."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path

sys.dont_write_bytecode = True

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

THIS = Path(__file__).resolve()
AI_TEAM = THIS.parents[1]
ROOT = AI_TEAM.parents[1]

sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(AI_TEAM))

ACTIVE_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "youngsuk_schedule": "schedule_manager.py",
    "somi": "somi_kis_reporter.py",
    "yewon_monitor": "harness_monitor.py",
}


def ok(msg: str) -> tuple[str, str]:
    return "OK", msg


def warn(msg: str) -> tuple[str, str]:
    return "WARN", msg


def fail(msg: str) -> tuple[str, str]:
    return "FAIL", msg


def read_json(path: Path):
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def age_text(path: Path) -> str:
    if not path.exists():
        return "missing"
    dt = datetime.fromtimestamp(path.stat().st_mtime)
    return dt.strftime("%m/%d %H:%M")


def git_lines(*args: str) -> list[str]:
    try:
        out = subprocess.run(
            ["git", "-C", str(ROOT), *args],
            capture_output=True,
            text=True,
            timeout=10,
        ).stdout
        return [line.strip().replace("\\", "/") for line in out.splitlines() if line.strip()]
    except Exception:
        return []


def git_tracked() -> list[str]:
    return git_lines("ls-files")


def git_tracked_ignored() -> list[str]:
    return git_lines("ls-files", "-ci", "--exclude-standard")


def git_untracked() -> list[str]:
    lines = git_lines("status", "--porcelain", "--untracked-files=all")
    return [line[3:].strip().replace("\\", "/") for line in lines if line.startswith("?? ")]


def find_python_pids(script_name: str) -> list[str]:
    try:
        if sys.platform == "darwin":
            out = subprocess.run(
                ["pgrep", "-f", script_name],
                capture_output=True,
                text=True,
                timeout=5,
            ).stdout
        else:
            cmd = (
                "Get-CimInstance Win32_Process | "
                "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
                f"$_.CommandLine.ToLower().Contains('{script_name.lower()}') }} | "
                "Select-Object -ExpandProperty ProcessId"
            )
            out = subprocess.run(
                ["powershell", "-NoProfile", "-Command", cmd],
                capture_output=True,
                text=True,
                timeout=5,
            ).stdout
        return [p for p in out.split() if p.isdigit()]
    except Exception:
        return []


def check_env():
    try:
        from _shared.env import load_env
        load_env()
    except Exception as e:
        return fail(f"load_env failed: {e}")

    required = ["TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID", "GEMINI_API_KEY"]
    missing = [k for k in required if not os.getenv(k)]
    if missing:
        return warn("missing env: " + ", ".join(missing))
    return ok("unified env loaded")


def check_runtime():
    try:
        from _shared.notify import agent_status
        status = agent_status()
    except Exception as e:
        return fail(f"runtime check failed: {e}")

    if not status:
        status = {name: ",".join(find_python_pids(script)) or "down" for name, script in ACTIVE_DAEMONS.items()}

    down = [k for k, v in status.items() if v == "down"]
    parts = [f"{k}={v}" for k, v in status.items()]
    return (warn if down else ok)("; ".join(parts))


def check_schedule():
    base = AI_TEAM / "skills" / "영숙_비서" / "tools"
    schedules = base / "schedules.json"
    last_run = base / "last_run.json"
    try:
        data = read_json(schedules)
        items = data.get("schedules", [])
        enabled = [s for s in items if s.get("enabled", True)]
    except Exception as e:
        return fail(f"schedule json failed: {e}")

    bad_commands = []
    for item in enabled:
        command = str(item.get("command", "")).strip()
        if command.startswith("python "):
            script = command.split()[1].replace("/", os.sep)
            if not (ROOT / script).exists():
                bad_commands.append(f"{item.get('id')}: {script}")
    if bad_commands:
        return fail("schedule command missing: " + ", ".join(bad_commands[:5]))

    last_info = age_text(last_run) if last_run.exists() else "never"
    return ok(f"enabled {len(enabled)}/{len(items)}, last_run {last_info}")


def check_trading():
    intel = ROOT / "reports" / "research" / "crypto_market_intel.json"
    if not intel.exists():
        return warn("missing crypto_market_intel.json")
    return ok(f"trading agents not installed in current tree; intel {age_text(intel)}")


def check_structure():
    required = [AI_TEAM / "_shared", AI_TEAM / "skills", AI_TEAM / "scripts", ROOT / "reports", ROOT / "output"]
    missing = [str(p.relative_to(ROOT)) for p in required if not p.exists()]
    if missing:
        return fail("missing dirs: " + ", ".join(missing))
    return ok("core dirs present")


def check_classification_layout():
    required_files = [
        ROOT / "AGENTS.md",
        ROOT / "PROJECT_OVERVIEW.md",
        ROOT / "README.md",
        ROOT / "CLAUDE.md",
        ROOT / "SKILL.md",
        ROOT / "docs" / "REPOSITORY_CLASSIFICATION.md",
        ROOT / "docs" / "TELEGRAM_BOT_README.md",
        AI_TEAM / "README.md",
        AI_TEAM / "scripts" / "README.md",
        AI_TEAM / "skills" / "README.md",
        AI_TEAM / "harness" / "check_all.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "calendar_manager.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "posting_scheduler.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "reports_manager.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "schedule_manager.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "telegram_receiver.py",
        AI_TEAM / "skills" / "영숙_비서" / "tools" / "upload_approval_flow.py",
        AI_TEAM / "skills" / "예원_CEO" / "tools" / "evaluate_feedback.py",
        AI_TEAM / "skills" / "예원_CEO" / "tools" / "skill_auditor.py",
        AI_TEAM / "skills" / "예원_CEO" / "tools" / "upload_manager.py",
        ROOT / "projects" / "petnna" / "index.html",
        ROOT / "projects" / "petnna" / "sw.js",
        ROOT / "projects" / "petnna" / "js" / "app.js",
        ROOT / "projects" / "petnna" / "js" / "state.js",
        ROOT / "projects" / "petnna" / "js" / "settings.js",
        ROOT / "projects" / "petnna" / "api" / "ai-health.js",
        AI_TEAM / "src" / "extension.ts",
    ]
    required_dirs = [
        ROOT / "docs" / "setup",
        ROOT / "docs" / "plans",
        ROOT / "docs" / "archive",
        ROOT / "reports",
        ROOT / "reports" / "research",
        ROOT / "reports" / "status",
        ROOT / "projects" / "petnna" / "docs",
        ROOT / "projects" / "petnna" / "api",
        ROOT / "projects" / "petnna" / "css",
        ROOT / "projects" / "petnna" / "js",
        ROOT / "projects" / "petnna" / "js" / "templates",
        AI_TEAM / "docs",
        AI_TEAM / "harness",
        AI_TEAM / "_shared",
        AI_TEAM / "skills" / "공용스킬",
        AI_TEAM / "assets" / "brain-seeds",
    ]

    missing = [str(p.relative_to(ROOT)) for p in required_files + required_dirs if not p.exists()]

    skills_dir = AI_TEAM / "skills"
    if skills_dir.exists():
        for agent_dir in sorted(p for p in skills_dir.iterdir() if p.is_dir()):
            if agent_dir.name.startswith(".") or agent_dir.name == "__pycache__":
                continue
            if agent_dir.name == "공용스킬":
                continue
            if not (agent_dir / "SKILL.md").exists():
                missing.append(str((agent_dir / "SKILL.md").relative_to(ROOT)))

    if missing:
        return fail("classification missing: " + ", ".join(missing[:10]))

    warnings = []
    for legacy_dir in [ROOT / ".backups", ROOT / ".archive"]:
        if legacy_dir.exists():
            warnings.append(f"legacy dir present: {legacy_dir.name}")

    project_reports = ROOT / "projects" / "reports"
    if project_reports.exists() and any(p.is_file() for p in project_reports.rglob("*")):
        warnings.append("misplaced reports: projects/reports")

    root_wrappers = sorted(
        p.name for p in ROOT.iterdir()
        if p.is_file() and p.suffix.lower() in {".bat", ".cmd", ".ps1"}
    )
    if root_wrappers:
        warnings.append("root wrappers present: " + ", ".join(root_wrappers[:8]))

    tracked = git_tracked()
    tracked_ignored = git_tracked_ignored()
    tracked_runtime = [
        p for p in tracked
        if p in {
            ".telegram_sent_cache.json",
            "reports/status/harness_latest.json",
            "reports/research/crypto_market_intel.json",
            "reports/research/hyunbin_alert_state.json",
            "projects/ai-team/skills/영숙_비서/tools/last_run.json",
        }
    ]
    tracked_plaintext_secrets = [
        p for p in tracked
        if p in {".env", "client_secret.json"} or p.endswith("/.env") or p.endswith("/client_secret.json")
    ]
    tracked_media = [
        p for p in tracked
        if p.startswith("output/") and Path(p).suffix.lower() in {".mp3", ".mp4", ".png", ".jpg", ".jpeg", ".webp"}
    ]
    if tracked_plaintext_secrets:
        warnings.append("tracked plaintext secrets: " + ", ".join(tracked_plaintext_secrets[:8]))
    if tracked_ignored:
        warnings.append("tracked ignored files: " + ", ".join(tracked_ignored[:8]))
    if tracked_runtime:
        warnings.append("tracked runtime state: " + ", ".join(tracked_runtime[:8]))
    if tracked_media:
        warnings.append("tracked generated media: " + ", ".join(tracked_media[:8]))

    if warnings:
        return warn(" | ".join(warnings))
    return ok("repository classification layout matches")


def check_report_layout():
    project_reports = AI_TEAM / "reports"
    if not project_reports.exists():
        return ok("no ai-team local reports")

    files = [p.relative_to(project_reports) for p in project_reports.rglob("*") if p.is_file()]
    if files:
        unexpected = sorted(str(p).replace("\\", "/") for p in files)
        return warn("unexpected ai-team reports: " + ", ".join(unexpected[:8]))
    return ok("ai-team local reports empty")


def check_root_layout():
    allowed_root_files = {
        ".codex/environments/environment.toml",
        ".env.encrypted",
        ".gitignore",
        "AGENTS.md",
        "CLAUDE.md",
        "ENV_MANIFEST.json",
        "PROJECT_OVERVIEW.md",
        "README.md",
        "SKILL.md",
        "client_secret.json.encrypted",
        "vercel.json",
    }
    tracked = git_tracked()
    root_files = [p for p in tracked if "/" not in p or p == ".codex/environments/environment.toml"]
    unexpected = sorted(p for p in root_files if p not in allowed_root_files)
    generated = sorted(p for p in tracked if p.startswith("output/"))
    problems = []
    if unexpected:
        problems.append("unexpected root files: " + ", ".join(unexpected[:8]))
    if generated:
        problems.append("tracked output files: " + ", ".join(generated[:8]))
    if problems:
        return warn(" | ".join(problems))
    return ok("root tracked files classified")


def check_docs_encoding():
    docs = [
        ROOT / "README.md",
        ROOT / "PROJECT_OVERVIEW.md",
        ROOT / "docs" / "REPOSITORY_CLASSIFICATION.md",
        AI_TEAM / "scripts" / "README.md",
        ROOT / "projects" / "petnna" / "README.md",
    ]
    markers = ["\ufffd", "\u8adb", "\u6028", "\u934d", "\u5a9b", "\uf9cf", "\u745c", "\uc48b"]
    bad = []
    for path in docs:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except Exception as e:
            bad.append(f"{path.relative_to(ROOT)} read failed: {e}")
            continue
        hits = sum(text.count(marker) for marker in markers)
        if hits:
            bad.append(f"{path.relative_to(ROOT)} markers={hits}")
    if bad:
        return warn("possible mojibake docs: " + ", ".join(bad[:8]))
    return ok("main docs encoding clean")


def check_unclassified_files():
    untracked = git_untracked()
    if untracked:
        return warn("unclassified untracked files: " + ", ".join(untracked[:8]))
    return ok("no unclassified untracked files")


def main() -> int:
    checks = {
        "env": check_env,
        "runtime": check_runtime,
        "schedule": check_schedule,
        "trading": check_trading,
        "structure": check_structure,
        "classification_layout": check_classification_layout,
        "report_layout": check_report_layout,
        "root_layout": check_root_layout,
        "docs_encoding": check_docs_encoding,
        "unclassified_files": check_unclassified_files,
    }
    worst = 0
    results = []
    for name, fn in checks.items():
        status, msg = fn()
        worst = max(worst, {"OK": 0, "WARN": 1, "FAIL": 2}[status])
        print(f"[{status}] {name}: {msg}")
        results.append({"name": name, "status": status, "message": msg})

    status_dir = ROOT / "reports" / "status"
    status_dir.mkdir(parents=True, exist_ok=True)
    overall = "FAIL" if worst == 2 else ("WARN" if worst == 1 else "OK")
    report = {
        "timestamp": datetime.now().isoformat(timespec="seconds"),
        "root": str(ROOT),
        "overall": overall,
        "checks": results,
    }
    (status_dir / "harness_latest.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"[OK] report: {status_dir / 'harness_latest.json'}")

    if worst >= 1:
        try:
            from _shared.notify import send
            issues = [r for r in results if r["status"] != "OK"]
            lines = [f"{r['status']} {r['name']}: {r['message'][:80]}" for r in issues]
            send(f"[?섎꽕?? {overall}\n" + "\n".join(lines))
        except Exception as e:
            print(f"[WARN] telegram notify failed: {e}")

    return worst


if __name__ == "__main__":
    raise SystemExit(main())

