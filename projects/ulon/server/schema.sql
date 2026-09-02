-- Ulon persist schema. SQLite와 PostgreSQL에서 같이 쓰도록 단순 타입만 사용.

CREATE TABLE IF NOT EXISTS accounts (
    account_id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS characters (
    character_id TEXT PRIMARY KEY,
    account_id TEXT NOT NULL,
    name TEXT NOT NULL,
    pos_x REAL NOT NULL DEFAULT 0,
    pos_y REAL NOT NULL DEFAULT 0,
    pos_z REAL NOT NULL DEFAULT 0,
    hp REAL NOT NULL DEFAULT 50,
    str REAL NOT NULL DEFAULT 30,
    dex REAL NOT NULL DEFAULT 25,
    intel REAL NOT NULL DEFAULT 25,
    str_lock INTEGER NOT NULL DEFAULT 0,
    dex_lock INTEGER NOT NULL DEFAULT 0,
    intel_lock INTEGER NOT NULL DEFAULT 0,
    appearance INTEGER NOT NULL DEFAULT 0,
    mana REAL NOT NULL DEFAULT 0,
    ghost INTEGER NOT NULL DEFAULT 0,
    gold INTEGER NOT NULL DEFAULT 0,
    fame INTEGER NOT NULL DEFAULT 0,
    karma INTEGER NOT NULL DEFAULT 0,
    notoriety INTEGER NOT NULL DEFAULT 0,
    murder_count INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS character_skills (
    character_id TEXT NOT NULL,
    skill_id INTEGER NOT NULL,
    value REAL NOT NULL DEFAULT 0,
    lock_state INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (character_id, skill_id)
);

CREATE TABLE IF NOT EXISTS inventories (
    owner_id TEXT NOT NULL,
    slot INTEGER NOT NULL,
    item_template TEXT NOT NULL,
    amount INTEGER NOT NULL DEFAULT 1,
    uses INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (owner_id, slot)
);

CREATE TABLE IF NOT EXISTS bank_items (
    owner_id TEXT NOT NULL,
    slot INTEGER NOT NULL,
    item_template TEXT NOT NULL,
    amount INTEGER NOT NULL DEFAULT 1,
    uses INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (owner_id, slot)
);

CREATE TABLE IF NOT EXISTS spellbook (
    character_id TEXT NOT NULL,
    spell_id INTEGER NOT NULL,
    PRIMARY KEY (character_id, spell_id)
);

CREATE TABLE IF NOT EXISTS corpses (
    owner_id TEXT PRIMARY KEY,
    corpse_id TEXT NOT NULL,
    pos_x REAL NOT NULL DEFAULT 0,
    pos_y REAL NOT NULL DEFAULT 0,
    pos_z REAL NOT NULL DEFAULT 0,
    death_time TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS corpse_items (
    corpse_id TEXT NOT NULL,
    slot INTEGER NOT NULL,
    item_template TEXT NOT NULL,
    amount INTEGER NOT NULL DEFAULT 1,
    uses INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (corpse_id, slot)
);
