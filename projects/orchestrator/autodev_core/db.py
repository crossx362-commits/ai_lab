"""SQLite 상태 저장소.

프로그램이 죽어도 상태가 남아야 한다(§14). 재시작만으로 Task가 DONE이 되는 일은 없다 —
DONE은 오직 Unity 판정을 거친 코드 경로에서만 쓰인다.
"""

from __future__ import annotations

import json
import sqlite3
import time
from pathlib import Path

from .config import STATE_DIR

DB_PATH = STATE_DIR / "autodev.sqlite3"

SCHEMA = """
CREATE TABLE IF NOT EXISTS tasks (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  goal          TEXT NOT NULL,
  target        TEXT NOT NULL,
  status        TEXT NOT NULL,           -- BACKLOG/READY/RUNNING/TESTING/BLOCKED/FAILED/DONE
  agent         TEXT,
  model         TEXT,
  branch        TEXT,
  worktree      TEXT,
  base_commit   TEXT,
  commit_hash   TEXT,
  verdict       TEXT,                    -- PASS/FAILED/UNKNOWN
  reason        TEXT,
  attempts      INTEGER NOT NULL DEFAULT 0,
  created_at    REAL NOT NULL,
  ended_at      REAL
);

CREATE TABLE IF NOT EXISTS plans (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  goal        TEXT NOT NULL,
  agent       TEXT,
  status      TEXT NOT NULL,           -- DRAFT/READY/RUNNING/DONE/BLOCKED
  dir         TEXT,
  note        TEXT,
  created_at  REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS attempts (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  task_id         INTEGER NOT NULL,
  n               INTEGER NOT NULL,
  agent           TEXT,
  model           TEXT,
  status          TEXT NOT NULL,         -- RUNNING/FAILED/PASS/UNKNOWN
  agent_exit      INTEGER,
  changed_files   INTEGER,
  changed_lines   INTEGER,
  compile_verdict TEXT,
  reason          TEXT,
  error_summary   TEXT,
  started_at      REAL NOT NULL,
  ended_at        REAL
);

CREATE TABLE IF NOT EXISTS processes (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  task_id     INTEGER,
  attempt_id  INTEGER,
  kind        TEXT NOT NULL,             -- agent/unity/git
  pid         INTEGER,
  pgid        INTEGER,
  cmd         TEXT NOT NULL,
  cwd         TEXT,
  status      TEXT NOT NULL,             -- RUNNING/EXITED/TIMEOUT/KILLED
  exit_code   INTEGER,
  stdout_path TEXT,
  stderr_path TEXT,
  started_at  REAL NOT NULL,
  ended_at    REAL
);

CREATE TABLE IF NOT EXISTS usage (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  task_id    INTEGER,
  attempt_id INTEGER,
  agent      TEXT,
  model      TEXT,
  ok         INTEGER,
  duration_s REAL,
  prompt_chars INTEGER,
  output_chars INTEGER,
  created_at REAL NOT NULL
);
"""


# 이미 만들어진 DB에 나중에 붙는 열. 지우고 다시 만들지 않는다 — 기록이 상태다.
MIGRATIONS = [
    ("tasks", "plan_id", "INTEGER"),
    ("tasks", "plan_key", "TEXT"),
    ("tasks", "depends_on", "TEXT"),
    ("tasks", "done_criteria", "TEXT"),
    ("tasks", "risk", "TEXT"),
    ("tasks", "review", "TEXT"),
]


def _migrate(conn) -> None:
    for table, col, decl in MIGRATIONS:
        cols = {r["name"] for r in conn.execute(f"PRAGMA table_info({table})")}
        if col not in cols:
            conn.execute(f"ALTER TABLE {table} ADD COLUMN {col} {decl}")
    conn.commit()


def connect(path: Path | None = None) -> sqlite3.Connection:
    p = path or DB_PATH
    p.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(p, timeout=30)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL")
    conn.executescript(SCHEMA)
    _migrate(conn)
    return conn


def now() -> float:
    return time.time()


# --- tasks ---------------------------------------------------------------


def create_task(conn, goal: str, target: str, agent: str, model: str | None, **extra) -> int:
    cols = ["goal", "target", "status", "agent", "model", "created_at"]
    vals = [goal, target, extra.pop("status", "READY"), agent, model, now()]
    for k, v in extra.items():
        cols.append(k)
        vals.append(v)
    ph = ",".join("?" * len(cols))
    cur = conn.execute(f"INSERT INTO tasks({','.join(cols)}) VALUES({ph})", vals)
    conn.commit()
    return int(cur.lastrowid)


# --- plans ---------------------------------------------------------------


def create_plan(conn, goal: str, agent: str, dir_: str) -> int:
    cur = conn.execute(
        "INSERT INTO plans(goal,agent,status,dir,created_at) VALUES(?,?,?,?,?)",
        (goal, agent, "DRAFT", dir_, now()),
    )
    conn.commit()
    return int(cur.lastrowid)


def update_plan(conn, plan_id: int, **fields) -> None:
    if not fields:
        return
    cols = ",".join(f"{k}=?" for k in fields)
    conn.execute(f"UPDATE plans SET {cols} WHERE id=?", (*fields.values(), plan_id))
    conn.commit()


def get_plan(conn, plan_id: int):
    return conn.execute("SELECT * FROM plans WHERE id=?", (plan_id,)).fetchone()


def list_plans(conn, limit: int = 20):
    return conn.execute("SELECT * FROM plans ORDER BY id DESC LIMIT ?", (limit,)).fetchall()


def plan_tasks(conn, plan_id: int):
    return conn.execute("SELECT * FROM tasks WHERE plan_id=? ORDER BY id", (plan_id,)).fetchall()


def update_task(conn, task_id: int, **fields) -> None:
    if not fields:
        return
    cols = ",".join(f"{k}=?" for k in fields)
    conn.execute(f"UPDATE tasks SET {cols} WHERE id=?", (*fields.values(), task_id))
    conn.commit()


def get_task(conn, task_id: int):
    return conn.execute("SELECT * FROM tasks WHERE id=?", (task_id,)).fetchone()


def list_tasks(conn, limit: int = 20):
    return conn.execute(
        "SELECT * FROM tasks ORDER BY id DESC LIMIT ?", (limit,)
    ).fetchall()


# --- attempts ------------------------------------------------------------


def create_attempt(conn, task_id: int, n: int, agent: str, model: str | None) -> int:
    cur = conn.execute(
        "INSERT INTO attempts(task_id,n,agent,model,status,started_at) VALUES(?,?,?,?,?,?)",
        (task_id, n, agent, model, "RUNNING", now()),
    )
    conn.commit()
    return int(cur.lastrowid)


def update_attempt(conn, attempt_id: int, **fields) -> None:
    if not fields:
        return
    cols = ",".join(f"{k}=?" for k in fields)
    conn.execute(f"UPDATE attempts SET {cols} WHERE id=?", (*fields.values(), attempt_id))
    conn.commit()


def list_attempts(conn, task_id: int):
    return conn.execute(
        "SELECT * FROM attempts WHERE task_id=? ORDER BY n", (task_id,)
    ).fetchall()


# --- processes -----------------------------------------------------------


def create_process(conn, *, task_id, attempt_id, kind, cmd, cwd, stdout_path, stderr_path) -> int:
    cur = conn.execute(
        "INSERT INTO processes(task_id,attempt_id,kind,cmd,cwd,status,stdout_path,stderr_path,started_at)"
        " VALUES(?,?,?,?,?,?,?,?,?)",
        (
            task_id,
            attempt_id,
            kind,
            json.dumps(cmd, ensure_ascii=False) if isinstance(cmd, list) else str(cmd),
            str(cwd),
            "RUNNING",
            str(stdout_path),
            str(stderr_path),
            now(),
        ),
    )
    conn.commit()
    return int(cur.lastrowid)


def update_process(conn, proc_id: int, **fields) -> None:
    if not fields:
        return
    cols = ",".join(f"{k}=?" for k in fields)
    conn.execute(f"UPDATE processes SET {cols} WHERE id=?", (*fields.values(), proc_id))
    conn.commit()


def running_processes(conn):
    return conn.execute("SELECT * FROM processes WHERE status='RUNNING'").fetchall()


# --- usage ---------------------------------------------------------------


def record_usage(conn, *, task_id, attempt_id, agent, model, ok, duration_s, prompt_chars, output_chars):
    conn.execute(
        "INSERT INTO usage(task_id,attempt_id,agent,model,ok,duration_s,prompt_chars,output_chars,created_at)"
        " VALUES(?,?,?,?,?,?,?,?,?)",
        (task_id, attempt_id, agent, model, 1 if ok else 0, duration_s, prompt_chars, output_chars, now()),
    )
    conn.commit()
