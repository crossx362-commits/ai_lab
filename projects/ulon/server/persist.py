#!/usr/bin/env python3
# Prefer server/.venv when launched from CharacterStore.
"""Ulon character persist. Prefers PostgreSQL, falls back to SQLite."""
from __future__ import annotations

import hashlib
import json
import os
import sqlite3
import traceback
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
DATA = ROOT / "data"
DATA.mkdir(parents=True, exist_ok=True)
DB_PATH = DATA / "ulon.sqlite"
SCHEMA = (HERE / "schema.sql").read_text(encoding="utf-8")
PORT = int(os.environ.get("ULON_PERSIST_PORT", "8777"))
DEFAULT_PG = "postgresql://ulon@127.0.0.1:5432/ulon"
SOURCE_SHA256 = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()


def _now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _pg_ok(url: str) -> bool:
    try:
        import psycopg2

        conn = psycopg2.connect(url, connect_timeout=1)
        conn.close()
        return True
    except Exception:
        return False


def pick_url() -> str:
    env = os.environ.get("DATABASE_URL", "").strip()
    if env:
        return env
    if _pg_ok(DEFAULT_PG):
        return DEFAULT_PG
    return ""


DATABASE_URL = pick_url()
POSTGRES = DATABASE_URL.startswith("postgres")


def connect():
    if POSTGRES:
        import psycopg2
        from psycopg2.extras import RealDictCursor

        conn = psycopg2.connect(DATABASE_URL)
        conn.autocommit = True
        conn.cursor_factory = RealDictCursor
        return conn
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn


def sql(text: str) -> str:
    return text.replace("?", "%s") if POSTGRES else text


def init():
    conn = connect()
    try:
        cur = conn.cursor()
        for stmt in SCHEMA.split(";"):
            s = stmt.strip()
            if s:
                cur.execute(s)
        migrate(cur)
        if not POSTGRES:
            conn.commit()
    finally:
        conn.close()


def migrate(cur):
    cols = (
        ("str", "REAL NOT NULL DEFAULT 30"),
        ("dex", "REAL NOT NULL DEFAULT 25"),
        ("intel", "REAL NOT NULL DEFAULT 25"),
        ("str_lock", "INTEGER NOT NULL DEFAULT 0"),
        ("dex_lock", "INTEGER NOT NULL DEFAULT 0"),
        ("intel_lock", "INTEGER NOT NULL DEFAULT 0"),
        ("appearance", "INTEGER NOT NULL DEFAULT 0"),
        ("mana", "REAL NOT NULL DEFAULT 0"),
        ("ghost", "INTEGER NOT NULL DEFAULT 0"),
        ("gold", "INTEGER NOT NULL DEFAULT 0"),
        ("fame", "INTEGER NOT NULL DEFAULT 0"),
        ("karma", "INTEGER NOT NULL DEFAULT 0"),
        ("notoriety", "INTEGER NOT NULL DEFAULT 0"),
        ("murder_count", "INTEGER NOT NULL DEFAULT 0"),
    )
    for name, decl in cols:
        try:
            if POSTGRES:
                cur.execute(f"ALTER TABLE characters ADD COLUMN IF NOT EXISTS {name} {decl}")
            else:
                cur.execute(f"ALTER TABLE characters ADD COLUMN {name} {decl}")
        except Exception:
            pass
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS bank_items (
            owner_id TEXT NOT NULL,
            slot INTEGER NOT NULL,
            item_template TEXT NOT NULL,
            amount INTEGER NOT NULL DEFAULT 1,
            PRIMARY KEY (owner_id, slot)
        )
        """
    )
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS spellbook (
            character_id TEXT NOT NULL,
            spell_id INTEGER NOT NULL,
            PRIMARY KEY (character_id, spell_id)
        )
        """
    )
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS corpses (
            owner_id TEXT PRIMARY KEY,
            corpse_id TEXT NOT NULL,
            pos_x REAL NOT NULL DEFAULT 0,
            pos_y REAL NOT NULL DEFAULT 0,
            pos_z REAL NOT NULL DEFAULT 0,
            death_time TEXT NOT NULL
        )
        """
    )
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS corpse_items (
            corpse_id TEXT NOT NULL,
            slot INTEGER NOT NULL,
            item_template TEXT NOT NULL,
            amount INTEGER NOT NULL DEFAULT 1,
            uses INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (corpse_id, slot)
        )
        """
    )
    for table in ("inventories", "bank_items", "corpse_items"):
        try:
            if POSTGRES:
                cur.execute(f"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS uses INTEGER NOT NULL DEFAULT 0")
            else:
                cur.execute(f"ALTER TABLE {table} ADD COLUMN uses INTEGER NOT NULL DEFAULT 0")
        except Exception:
            pass
        try:
            if POSTGRES:
                cur.execute(f"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS maker_id TEXT NOT NULL DEFAULT ''")
            else:
                cur.execute(f"ALTER TABLE {table} ADD COLUMN maker_id TEXT NOT NULL DEFAULT ''")
        except Exception:
            pass


    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS houses (
            plot_id TEXT PRIMARY KEY,
            owner_character_id TEXT NOT NULL DEFAULT '',
            account_id TEXT NOT NULL DEFAULT '',
            public_flag INTEGER NOT NULL DEFAULT 0
        )
        """
    )
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS house_items (
            plot_id TEXT NOT NULL,
            slot INTEGER NOT NULL,
            item_template TEXT NOT NULL,
            amount INTEGER NOT NULL DEFAULT 1,
            uses INTEGER NOT NULL DEFAULT 0,
            maker_id TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (plot_id, slot)
        )
        """
    )
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS stables (
            character_id TEXT PRIMARY KEY,
            pet_id TEXT NOT NULL DEFAULT '',
            control_slots INTEGER NOT NULL DEFAULT 1,
            display_name TEXT NOT NULL DEFAULT ''
        )
        """
    )


EX_PREFIX = "EX:"


def _truthy(v):
    return v is True or v == 1 or v == "true" or v == "True"


def _pack_maker(it):
    if not isinstance(it, dict):
        return ""
    maker = str(it.get("makerId", it.get("MakerId", "")) or "")
    exceptional = _truthy(it.get("exceptional", it.get("Exceptional", False)))
    if exceptional and not maker.startswith(EX_PREFIX):
        return EX_PREFIX + maker
    return maker


def _item(r):
    uses = 0
    try:
        uses = int(r["uses"] or 0)
    except Exception:
        uses = 0
    maker = ""
    try:
        maker = str(r["maker_id"] or "")
    except Exception:
        maker = ""
    exceptional = False
    if maker.startswith(EX_PREFIX):
        exceptional = True
        maker = maker[len(EX_PREFIX):]
    return {
        "Slot": r["slot"],
        "TemplateId": r["item_template"],
        "Amount": r["amount"],
        "Uses": uses,
        "MakerId": maker,
        "Exceptional": exceptional,
    }


def _one(cur):
    row = cur.fetchone()
    if row is None:
        return None
    if isinstance(row, dict):
        return row
    cols = [d[0] for d in cur.description]
    return dict(zip(cols, row))


def _all(cur):
    rows = cur.fetchall()
    out = []
    for row in rows:
        if isinstance(row, dict):
            out.append(row)
        else:
            cols = [d[0] for d in cur.description]
            out.append(dict(zip(cols, row)))
    return out


def get_character(account_id: str) -> dict | None:
    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(sql("SELECT * FROM characters WHERE account_id = ?"), (account_id,))
        char = _one(cur)
        if char is None:
            return None
        cur.execute(
            sql("SELECT skill_id, value, lock_state FROM character_skills WHERE character_id = ?"),
            (char["character_id"],),
        )
        skills = [{"Id": r["skill_id"], "Value": r["value"], "Lock": r["lock_state"]} for r in _all(cur)]
        cur.execute(
            sql("SELECT slot, item_template, amount, uses, maker_id FROM inventories WHERE owner_id = ?"),
            (char["character_id"],),
        )
        inv = [_item(r) for r in _all(cur)]
        cur.execute(
            sql("SELECT slot, item_template, amount, uses, maker_id FROM bank_items WHERE owner_id = ?"),
            (char["character_id"],),
        )
        bank = [_item(r) for r in _all(cur)]
        cur.execute(sql("SELECT spell_id FROM spellbook WHERE character_id = ?"), (char["character_id"],))
        spells = [int(r["spell_id"]) for r in _all(cur)]
        cur.execute(sql("SELECT corpse_id, pos_x, pos_y, pos_z FROM corpses WHERE owner_id = ?"), (char["character_id"],))
        corpse_row = _one(cur)
        corpse_id = ""
        cx = cy = cz = 0.0
        corpse_items = []
        if corpse_row is not None:
            corpse_id = str(corpse_row["corpse_id"])
            cx = float(corpse_row["pos_x"])
            cy = float(corpse_row["pos_y"])
            cz = float(corpse_row["pos_z"])
            cur.execute(
                sql("SELECT slot, item_template, amount, uses, maker_id FROM corpse_items WHERE corpse_id = ?"),
                (corpse_id,),
            )
            corpse_items = [_item(r) for r in _all(cur)]
        return {
            "AccountId": char["account_id"],
            "CharacterId": char["character_id"],
            "Name": char["name"],
            "X": char["pos_x"],
            "Y": char["pos_y"],
            "Z": char["pos_z"],
            "Hp": char["hp"],
            "Str": int(char.get("str") or 30),
            "Dex": int(char.get("dex") or 25),
            "Int": int(char.get("intel") or 25),
            "StrLock": int(char.get("str_lock") or 0),
            "DexLock": int(char.get("dex_lock") or 0),
            "IntLock": int(char.get("intel_lock") or 0),
            "Appearance": int(char.get("appearance") or 0),
            "Mana": float(char.get("mana") or 0),
            "Ghost": int(char.get("ghost") or 0) != 0,
            "Gold": int(char.get("gold") or 0),
            "Fame": int(char.get("fame") or 0),
            "Karma": int(char.get("karma") or 0),
            "Notoriety": int(char.get("notoriety") or 0),
            "MurderCount": int(char.get("murder_count") or 0),
            "Spells": spells,
            "Skills": skills,
            "Inventory": inv,
            "Bank": bank,
            "CorpseId": corpse_id,
            "CorpseX": cx,
            "CorpseY": cy,
            "CorpseZ": cz,
            "Corpse": corpse_items,
        }
    finally:
        conn.close()


def put_character(body: dict) -> dict:
    account = str(body.get("accountId") or body.get("AccountId") or "")
    if not account:
        raise ValueError("accountId required")
    character_id = str(body.get("characterId") or body.get("CharacterId") or account)
    name = str(body.get("name") or body.get("Name") or "나")
    x = float(body.get("x", body.get("X", 0)))
    y = float(body.get("y", body.get("Y", 0)))
    z = float(body.get("z", body.get("Z", 0)))
    hp = float(body.get("hp", body.get("Hp", 50)))
    strength = int(body.get("str", body.get("Str", 30)))
    dex = int(body.get("dex", body.get("Dex", 25)))
    intel = int(body.get("int", body.get("Int", 25)))
    str_lock = int(body.get("strLock", body.get("StrLock", 0)))
    dex_lock = int(body.get("dexLock", body.get("DexLock", 0)))
    int_lock = int(body.get("intLock", body.get("IntLock", 0)))
    appearance = int(body.get("appearance", body.get("Appearance", 0)))
    mana = float(body.get("mana", body.get("Mana", 0)))
    ghost = 1 if body.get("ghost", body.get("Ghost", False)) else 0
    gold = int(body.get("gold", body.get("Gold", 0)))
    fame = int(body.get("fame", body.get("Fame", 0)))
    karma = int(body.get("karma", body.get("Karma", 0)))
    notoriety = int(body.get("notoriety", body.get("Notoriety", 0)))
    murder_count = int(body.get("murderCount", body.get("MurderCount", 0)))
    skills = body.get("skills") or body.get("Skills") or []
    inventory = body.get("inventory") or body.get("Inventory") or []
    bank = body.get("bank") or body.get("Bank") or []
    spells = body.get("spells") or body.get("Spells") or []
    corpse = body.get("corpse") or body.get("Corpse") or []
    corpse_id = str(body.get("corpseId") or body.get("CorpseId") or "")
    corpse_x = float(body.get("corpseX", body.get("CorpseX", 0)))
    corpse_y = float(body.get("corpseY", body.get("CorpseY", 0)))
    corpse_z = float(body.get("corpseZ", body.get("CorpseZ", 0)))

    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(
            sql("INSERT INTO accounts(account_id, created_at) VALUES (?, ?) ON CONFLICT (account_id) DO NOTHING"),
            (account, _now()),
        )
        cur.execute(
            sql(
                """
                INSERT INTO characters(character_id, account_id, name, pos_x, pos_y, pos_z, hp, str, dex, intel, str_lock, dex_lock, intel_lock, appearance, mana, ghost, gold, fame, karma, notoriety, murder_count)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(character_id) DO UPDATE SET
                    name=excluded.name, pos_x=excluded.pos_x, pos_y=excluded.pos_y,
                    pos_z=excluded.pos_z, hp=excluded.hp,
                    str=excluded.str, dex=excluded.dex, intel=excluded.intel,
                    str_lock=excluded.str_lock, dex_lock=excluded.dex_lock, intel_lock=excluded.intel_lock,
                    appearance=excluded.appearance, mana=excluded.mana, ghost=excluded.ghost, gold=excluded.gold,
                    fame=excluded.fame, karma=excluded.karma, notoriety=excluded.notoriety, murder_count=excluded.murder_count
                """
            ),
            (character_id, account, name, x, y, z, hp, strength, dex, intel, str_lock, dex_lock, int_lock, appearance, mana, ghost, gold, fame, karma, notoriety, murder_count),
        )
        cur.execute(sql("DELETE FROM character_skills WHERE character_id = ?"), (character_id,))
        for s in skills:
            cur.execute(
                sql("INSERT INTO character_skills(character_id, skill_id, value, lock_state) VALUES (?, ?, ?, ?)"),
                (
                    character_id,
                    int(s.get("id", s.get("Id", 0))),
                    float(s.get("value", s.get("Value", 0))),
                    int(s.get("lock", s.get("Lock", 0))),
                ),
            )
        cur.execute(sql("DELETE FROM inventories WHERE owner_id = ?"), (character_id,))
        for it in inventory:
            cur.execute(
                sql("INSERT INTO inventories(owner_id, slot, item_template, amount, uses, maker_id) VALUES (?, ?, ?, ?, ?, ?)"),
                (
                    character_id,
                    int(it.get("slot", it.get("Slot", 0))),
                    str(it.get("templateId", it.get("TemplateId", ""))),
                    int(it.get("amount", it.get("Amount", 1))),
                    int(it.get("uses", it.get("Uses", 0))),
                    _pack_maker(it),
                ),
            )
        cur.execute(sql("DELETE FROM bank_items WHERE owner_id = ?"), (character_id,))
        for it in bank:
            cur.execute(
                sql("INSERT INTO bank_items(owner_id, slot, item_template, amount, uses, maker_id) VALUES (?, ?, ?, ?, ?, ?)"),
                (
                    character_id,
                    int(it.get("slot", it.get("Slot", 0))),
                    str(it.get("templateId", it.get("TemplateId", ""))),
                    int(it.get("amount", it.get("Amount", 1))),
                    int(it.get("uses", it.get("Uses", 0))),
                    _pack_maker(it),
                ),
            )
        cur.execute(sql("DELETE FROM spellbook WHERE character_id = ?"), (character_id,))
        for s in spells:
            sid = int(s.get("id", s.get("Id", s))) if isinstance(s, dict) else int(s)
            cur.execute(sql("INSERT INTO spellbook(character_id, spell_id) VALUES (?, ?)"), (character_id, sid))
        cur.execute(sql("SELECT corpse_id FROM corpses WHERE owner_id = ?"), (character_id,))
        old_c = _one(cur)
        if old_c is not None:
            cur.execute(sql("DELETE FROM corpse_items WHERE corpse_id = ?"), (old_c["corpse_id"],))
        cur.execute(sql("DELETE FROM corpses WHERE owner_id = ?"), (character_id,))
        if corpse_id:
            cur.execute(
                sql("INSERT INTO corpses(owner_id, corpse_id, pos_x, pos_y, pos_z, death_time) VALUES (?, ?, ?, ?, ?, ?)"),
                (character_id, corpse_id, corpse_x, corpse_y, corpse_z, _now()),
            )
            for it in corpse:
                cur.execute(
                    sql("INSERT INTO corpse_items(corpse_id, slot, item_template, amount, uses, maker_id) VALUES (?, ?, ?, ?, ?, ?)"),
                    (
                        corpse_id,
                        int(it.get("slot", it.get("Slot", 0))) if isinstance(it, dict) else 0,
                        str(it.get("templateId", it.get("TemplateId", ""))) if isinstance(it, dict) else "",
                        int(it.get("amount", it.get("Amount", 1))) if isinstance(it, dict) else 1,
                        int(it.get("uses", it.get("Uses", 0))) if isinstance(it, dict) else 0,
                        _pack_maker(it) if isinstance(it, dict) else "",
                    ),
                )
        if not POSTGRES:
            conn.commit()
    finally:
        conn.close()
    return get_character(account) or {}



def get_house(plot_id: str) -> dict:
    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(sql("SELECT plot_id, owner_character_id, account_id, public_flag FROM houses WHERE plot_id = ?"), (plot_id,))
        row = _one(cur)
        if row is None:
            return {
                "PlotId": plot_id,
                "OwnerCharacterId": "",
                "AccountId": "",
                "PublicFlag": 0,
                "Items": [],
            }
        cur.execute(
            sql("SELECT slot, item_template, amount, uses, maker_id FROM house_items WHERE plot_id = ? ORDER BY slot"),
            (plot_id,),
        )
        items = [_item(r) for r in _all(cur)]
        return {
            "PlotId": row["plot_id"],
            "OwnerCharacterId": row["owner_character_id"] or "",
            "AccountId": row["account_id"] or "",
            "PublicFlag": int(row["public_flag"] or 0),
            "Items": items,
        }
    finally:
        conn.close()


def put_house(plot_id: str, body: dict) -> dict:
    owner = str(body.get("ownerCharacterId") or body.get("OwnerCharacterId") or "")
    account = str(body.get("accountId") or body.get("AccountId") or "")
    public_flag = int(body.get("publicFlag", body.get("PublicFlag", 0)))
    items = body.get("items") or body.get("Items") or []
    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(
            sql(
                """
                INSERT INTO houses(plot_id, owner_character_id, account_id, public_flag)
                VALUES (?, ?, ?, ?)
                ON CONFLICT(plot_id) DO UPDATE SET
                    owner_character_id=excluded.owner_character_id,
                    account_id=excluded.account_id,
                    public_flag=excluded.public_flag
                """
            ),
            (plot_id, owner, account, public_flag),
        )
        cur.execute(sql("DELETE FROM house_items WHERE plot_id = ?"), (plot_id,))
        for it in items:
            cur.execute(
                sql("INSERT INTO house_items(plot_id, slot, item_template, amount, uses, maker_id) VALUES (?, ?, ?, ?, ?, ?)"),
                (
                    plot_id,
                    int(it.get("slot", it.get("Slot", 0))),
                    str(it.get("templateId", it.get("TemplateId", ""))),
                    int(it.get("amount", it.get("Amount", 1))),
                    int(it.get("uses", it.get("Uses", 0))),
                    _pack_maker(it),
                ),
            )
        if not POSTGRES:
            conn.commit()
    finally:
        conn.close()
    return get_house(plot_id)


def get_stable(character_id: str) -> dict:
    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(
            sql("SELECT character_id, pet_id, control_slots, display_name FROM stables WHERE character_id = ?"),
            (character_id,),
        )
        row = _one(cur)
        if row is None:
            return {
                "CharacterId": character_id,
                "PetId": "",
                "ControlSlots": 1,
                "DisplayName": "",
            }
        return {
            "CharacterId": row["character_id"] or character_id,
            "PetId": row["pet_id"] or "",
            "ControlSlots": int(row["control_slots"] or 1),
            "DisplayName": row["display_name"] or "",
        }
    finally:
        conn.close()


def put_stable(character_id: str, body: dict) -> dict:
    pet_id = str(body.get("petId") or body.get("PetId") or "")
    slots = int(body.get("controlSlots", body.get("ControlSlots", 1)) or 1)
    display = str(body.get("displayName") or body.get("DisplayName") or "")
    conn = connect()
    try:
        cur = conn.cursor()
        cur.execute(
            sql(
                """
                INSERT INTO stables(character_id, pet_id, control_slots, display_name)
                VALUES (?, ?, ?, ?)
                ON CONFLICT(character_id) DO UPDATE SET
                    pet_id=excluded.pet_id,
                    control_slots=excluded.control_slots,
                    display_name=excluded.display_name
                """
            ),
            (character_id, pet_id, slots, display),
        )
        if not POSTGRES:
            conn.commit()
    finally:
        conn.close()
    return get_stable(character_id)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        print("[persist]", fmt % args, flush=True)

    def _send(self, code, obj):
        data = json.dumps(obj, ensure_ascii=False).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path in ("/health", "/ready"):
            self._send(
                200,
                {
                    "ok": True,
                    "driver": "postgres" if POSTGRES else "sqlite",
                    "port": PORT,
                    "source_sha256": SOURCE_SHA256,
                    "time": _now(),
                },
            )
            return
        if self.path.startswith("/character/"):
            account = unquote(self.path.split("/character/", 1)[1].strip("/"))
            found = get_character(account)
            if found is None:
                self._send(404, {"ok": False, "message": "not found"})
                return
            self._send(200, found)
            return
        if self.path.startswith("/house/"):
            plot_id = unquote(self.path.split("/house/", 1)[1].strip("/"))
            self._send(200, get_house(plot_id))
            return
        if self.path.startswith("/stable/"):
            character_id = unquote(self.path.split("/stable/", 1)[1].strip("/"))
            self._send(200, get_stable(character_id))
            return
        self._send(404, {"ok": False})

    def do_PUT(self):
        if self.path.startswith("/house/"):
            length = int(self.headers.get("Content-Length", "0") or 0)
            raw = self.rfile.read(length) if length else b"{}"
            try:
                body = json.loads(raw.decode() or "{}")
                plot_id = unquote(self.path.split("/house/", 1)[1].strip("/"))
                saved = put_house(plot_id, body)
                self._send(200, saved)
            except Exception as e:
                traceback.print_exc()
                self._send(400, {"ok": False, "message": str(e)})
            return
        if self.path.startswith("/stable/"):
            length = int(self.headers.get("Content-Length", "0") or 0)
            raw = self.rfile.read(length) if length else b"{}"
            try:
                body = json.loads(raw.decode() or "{}")
                character_id = unquote(self.path.split("/stable/", 1)[1].strip("/"))
                body["characterId"] = body.get("characterId") or body.get("CharacterId") or character_id
                saved = put_stable(character_id, body)
                self._send(200, saved)
            except Exception as e:
                traceback.print_exc()
                self._send(400, {"ok": False, "message": str(e)})
            return
        if not self.path.startswith("/character/"):
            self._send(404, {"ok": False})
            return
        length = int(self.headers.get("Content-Length", "0") or 0)
        raw = self.rfile.read(length) if length else b"{}"
        try:
            body = json.loads(raw.decode() or "{}")
            account = unquote(self.path.split("/character/", 1)[1].strip("/"))
            body["accountId"] = body.get("accountId") or account
            saved = put_character(body)
            self._send(200, saved)
        except Exception as e:
            traceback.print_exc()
            self._send(400, {"ok": False, "message": str(e)})


def main():
    init()
    host = "127.0.0.1"
    server = ThreadingHTTPServer((host, PORT), Handler)
    db = DATABASE_URL if POSTGRES else str(DB_PATH)
    print(f"[persist] http://{host}:{PORT} driver={'postgres' if POSTGRES else 'sqlite'} db={db}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
