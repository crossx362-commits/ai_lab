#!/usr/bin/env python3
"""자율 개발 루프의 로컬 런타임 상태를 원자적으로 관리한다."""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
import time
from typing import Any


PHASES = ("running", "quota_wait", "recovering", "owner_stopped")
DEFAULT_STATE: dict[str, Any] = {
    "phase": "owner_stopped",
    "provider": "",
    "heartbeat_at": 0,
    "reason": "",
    "retry_at": 0,
    "last_error_fingerprint": "",
    "recovery_claims": [],
}
QUOTA_RE = re.compile(
    r"usage limit|quota exceeded|rate limit exceeded|out of credits|"
    r"할당량\s*초과|사용량.*(?:소진|초과)",
    re.IGNORECASE,
)


def read_state(path: Path) -> dict[str, Any]:
    data: dict[str, Any] = {}
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(value, dict):
            data = value
    except (FileNotFoundError, json.JSONDecodeError, OSError):
        pass
    result = dict(DEFAULT_STATE)
    result.update(data)
    if result.get("phase") not in PHASES:
        result["phase"] = "owner_stopped"
    if not isinstance(result.get("recovery_claims"), list):
        result["recovery_claims"] = []
    return result


def _write_atomic(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_name = ""
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            dir=path.parent,
            prefix=f"{path.name}.",
            suffix=".tmp",
            delete=False,
        ) as handle:
            tmp_name = handle.name
            json.dump(data, handle, ensure_ascii=False, indent=2)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(tmp_name, path)
    finally:
        if tmp_name:
            try:
                Path(tmp_name).unlink()
            except FileNotFoundError:
                pass


def _locked_update(path: Path, transform) -> dict[str, Any]:
    path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = path.with_suffix(path.suffix + ".lock")
    with lock_path.open("a+", encoding="utf-8") as lock:
        fcntl.flock(lock.fileno(), fcntl.LOCK_EX)
        data = read_state(path)
        transform(data)
        _write_atomic(path, data)
        return data


def update_state(path: Path, **changes: Any) -> dict[str, Any]:
    phase = changes.get("phase")
    if phase is not None and phase not in PHASES:
        raise ValueError(f"알 수 없는 phase: {phase}")

    def apply(data: dict[str, Any]) -> None:
        data.update({key: value for key, value in changes.items() if value is not None})
        data["heartbeat_at"] = int(time.time())

    return _locked_update(path, apply)


def heartbeat(path: Path) -> dict[str, Any]:
    return update_state(path)


def classify_failure(log_tail: str, exit_code: int) -> str:
    if exit_code == 0:
        return "ok"
    return "quota" if QUOTA_RE.search(log_tail) else "error"


def error_fingerprint(
    provider: str,
    exit_code: int,
    log_tail: str,
    context_version: str,
) -> str:
    tail = "\n".join(log_tail.splitlines()[-80:]).strip()
    raw = json.dumps(
        [provider, int(exit_code), tail, context_version],
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def claim_recovery(path: Path, fingerprint: str) -> bool:
    claimed = False

    def apply(data: dict[str, Any]) -> None:
        nonlocal claimed
        claims = [str(value) for value in data.get("recovery_claims", [])]
        if fingerprint in claims:
            return
        claims.append(fingerprint)
        data["recovery_claims"] = claims[-32:]
        data["last_error_fingerprint"] = fingerprint
        data["heartbeat_at"] = int(time.time())
        claimed = True

    _locked_update(path, apply)
    return claimed


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--path", required=True, type=Path)
    commands = parser.add_subparsers(dest="command", required=True)

    set_cmd = commands.add_parser("set")
    set_cmd.add_argument("phase", choices=PHASES)
    set_cmd.add_argument("--provider")
    set_cmd.add_argument("--reason")
    set_cmd.add_argument("--retry-at", type=int)

    commands.add_parser("heartbeat")

    get_cmd = commands.add_parser("get")
    get_cmd.add_argument("field", choices=tuple(DEFAULT_STATE))

    classify_cmd = commands.add_parser("classify")
    classify_cmd.add_argument("--log", required=True, type=Path)
    classify_cmd.add_argument("--exit-code", required=True, type=int)

    fingerprint_cmd = commands.add_parser("fingerprint")
    fingerprint_cmd.add_argument("--provider", required=True)
    fingerprint_cmd.add_argument("--exit-code", required=True, type=int)
    fingerprint_cmd.add_argument("--log", required=True, type=Path)
    fingerprint_cmd.add_argument("--context-version", required=True)

    claim_cmd = commands.add_parser("claim")
    claim_cmd.add_argument("fingerprint")
    return parser


def main() -> int:
    args = _parser().parse_args()
    if args.command == "set":
        value = update_state(
            args.path,
            phase=args.phase,
            provider=args.provider,
            reason=args.reason,
            retry_at=args.retry_at,
        )
        print(json.dumps(value, ensure_ascii=False))
        return 0
    if args.command == "heartbeat":
        print(json.dumps(heartbeat(args.path), ensure_ascii=False))
        return 0
    if args.command == "get":
        value = read_state(args.path)[args.field]
        print(json.dumps(value, ensure_ascii=False) if isinstance(value, (dict, list)) else value)
        return 0
    if args.command == "classify":
        text = args.log.read_text(encoding="utf-8", errors="replace") if args.log.is_file() else ""
        print(classify_failure(text, args.exit_code))
        return 0
    if args.command == "fingerprint":
        text = args.log.read_text(encoding="utf-8", errors="replace") if args.log.is_file() else ""
        print(error_fingerprint(args.provider, args.exit_code, text, args.context_version))
        return 0
    if args.command == "claim":
        return 0 if claim_recovery(args.path, args.fingerprint) else 1
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
